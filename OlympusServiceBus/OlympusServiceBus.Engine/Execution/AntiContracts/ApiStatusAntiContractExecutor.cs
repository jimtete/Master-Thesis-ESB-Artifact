using System.Text;
using System.Text.Json.Nodes;
using OlympusServiceBus.Utils.Contracts.AntiContracts;

namespace OlympusServiceBus.Engine.Execution.AntiContracts;

public class ApiStatusAntiContractExecutor : IAntiContractExecutor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiStatusAntiContractExecutor> _logger;

    public ApiStatusAntiContractExecutor(
        HttpClient httpClient, 
        ILogger<ApiStatusAntiContractExecutor> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool CanExecute(AntiContractBase antiContract)
    {
        return antiContract is ApiStatusAntiContract;
    }

    public async Task ExecuteAsync(
        AntiContractBase antiContract,
        AntiContractExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (antiContract is not ApiStatusAntiContract contract)
        {
            throw new InvalidOperationException(
                $"Unsupported anti-contract type: {antiContract.GetType().Name}");
        }

        var payload = BuildPayload(contract, context);
        var endpoint = contract.Endpoint;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                $"Anti-contract '{contract.ContractId}' does not define an endpoint.");
        }

        using var request = new HttpRequestMessage(
            new HttpMethod(contract.Method ?? "POST"),
            endpoint);

        request.Content = new StringContent(
            payload.ToJsonString(),
            Encoding.UTF8,
            "application/json");

        _logger.LogInformation(
            "Executing ApiStatusAntiContract {ContractId} for source contract {SourceContractId} with business key {BusinessKey}",
            contract.ContractId,
            context.SourceContractId,
            context.BusinessKey);

        using var cts = CreateCancellationTokenSource(contract.TimeoutSeconds, cancellationToken);

        var response = await _httpClient.SendAsync(request, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

            _logger.LogWarning(
                "Anti-contract {ContractId} failed with status code {StatusCode}. Response: {ResponseBody}",
                contract.ContractId,
                (int)response.StatusCode,
                responseBody);

            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation(
            "Anti-contract {ContractId} executed successfully for business key {BusinessKey}",
            contract.ContractId,
            context.BusinessKey);
    }
    
    private static JsonObject BuildPayload(
        ApiStatusAntiContract contract,
        AntiContractExecutionContext context)
    {
        var payload = new JsonObject();

        foreach (var pair in contract.StaticPayload)
        {
            payload[pair.Key] = pair.Value;
        }

        foreach (var pair in contract.PayloadMappings)
        {
            var value = ResolveMappedValue(pair.Value, context);
            payload[pair.Key] = value;
        }

        var normalizedStatus = ResolveMappedStatus(contract, context.ExecutionStatus);
        if (!string.IsNullOrWhiteSpace(normalizedStatus) && payload["status"] is null)
        {
            payload["status"] = normalizedStatus;
        }

        if (payload["businessKey"] is null && !string.IsNullOrWhiteSpace(context.BusinessKey))
        {
            payload["businessKey"] = context.BusinessKey;
        }

        if (payload["sourceContractId"] is null && !string.IsNullOrWhiteSpace(context.SourceContractId))
        {
            payload["sourceContractId"] = context.SourceContractId;
        }

        if (payload["completedAtUtc"] is null)
        {
            payload["completedAtUtc"] = context.CompletedAtUtc;
        }

        return payload;
    }
    
    private static string? ResolveMappedValue(string expression, AntiContractExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        return expression switch
        {
            "$context.sourceContractId" => context.SourceContractId,
            "$context.sourceContractName" => context.SourceContractName,
            "$context.businessKey" => context.BusinessKey,
            "$context.executionStatus" => context.ExecutionStatus,
            "$context.errorMessage" => context.ErrorMessage,
            "$context.errorCode" => context.ErrorCode,
            "$context.completedAtUtc" => context.CompletedAtUtc.ToString("O"),
            _ when expression.StartsWith("$correlation.", StringComparison.OrdinalIgnoreCase)
                => ResolveCorrelationValue(expression, context),
            _ when expression.StartsWith("$.", StringComparison.Ordinal)
                => ResolveJsonPathLikeValue(expression, context),
            _ => expression
        };
    }

    private static string? ResolveCorrelationValue(string expression, AntiContractExecutionContext context)
    {
        var key = expression["$correlation.".Length..];

        return context.CorrelationValues.TryGetValue(key, out var value)
            ? value
            : null;
    }

    private static string? ResolveJsonPathLikeValue(string expression, AntiContractExecutionContext context)
    {
        if (expression.StartsWith("$.original.", StringComparison.OrdinalIgnoreCase))
        {
            return ReadJsonValue(context.OriginalPayload, expression["$.original.".Length..]);
        }

        if (expression.StartsWith("$.transformed.", StringComparison.OrdinalIgnoreCase))
        {
            return ReadJsonValue(context.TransformedPayload, expression["$.transformed.".Length..]);
        }

        if (expression.StartsWith("$.response.", StringComparison.OrdinalIgnoreCase))
        {
            return ReadJsonValue(context.ResponsePayload, expression["$.response.".Length..]);
        }

        return null;
        
    }

    private static string? ReadJsonValue(JsonObject? root, string path)
    {
        if (root is null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        JsonNode? current = root;

        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current?[segment];
            if (current is null)
            {
                return null;
            }
        }
        
        return current?.ToString();
    }

    private static string ResolveMappedStatus(ApiStatusAntiContract contract, string executionStatus)
    {
        if (string.IsNullOrWhiteSpace(executionStatus))
        {
            return executionStatus;
        }
        
        return contract.StatusMappings.TryGetValue(executionStatus, out var mapped)
            ? mapped
            : executionStatus;
    }

    private static CancellationTokenSource CreateCancellationTokenSource(
        int? timeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (timeoutSeconds is null || timeoutSeconds <= 0)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds.Value));
        return cts;
    }
}