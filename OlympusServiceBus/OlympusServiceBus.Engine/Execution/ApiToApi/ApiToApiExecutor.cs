using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OlympusServiceBus.Engine.Execution.Transformation;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.ApiToApi;

public class ApiToApiExecutor
{
    private readonly ILogger<ApiToApiExecutor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMappingEngine _mappingEngine;

    public ApiToApiExecutor(
        ILogger<ApiToApiExecutor> logger,
        IHttpClientFactory httpClientFactory,
        IMappingEngine mappingEngine)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _mappingEngine = mappingEngine;
    }

    public async Task<ApiToApiExecutionResult?> BuildExecutionAsync(
        ApiToApiContract contract,
        CancellationToken cancellationToken)
    {
        if (!contract.Enabled)
            return null;

        if (string.IsNullOrWhiteSpace(contract.Source?.Endpoint) ||
            string.IsNullOrWhiteSpace(contract.Sink?.Endpoint))
        {
            _logger.LogWarning("[{Contract}] Missing Source.Endpoint or Sink.Endpoint.", contract.ContractId);
            return null;
        }

        contract.Source.Method ??= "GET";
        contract.Sink.Method ??= "POST";

        if (!contract.Source.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "[{Contract}] PoC supports Source.Method=GET only. Found: {Method}",
                contract.ContractId,
                contract.Source.Method);

            return null;
        }

        var client = _httpClientFactory.CreateClient();

        JsonObject? sourceObj;
        try
        {
            using var resp = await client.GetAsync(contract.Source.Endpoint, cancellationToken);
            resp.EnsureSuccessStatusCode();

            var text = await resp.Content.ReadAsStringAsync(cancellationToken);
            sourceObj = JsonNode.Parse(text) as JsonObject;

            if (sourceObj is null)
            {
                _logger.LogWarning("[{Contract}] Source returned non-object JSON.", contract.ContractId);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{Contract}] Source call failed: {Endpoint}",
                contract.ContractId,
                contract.Source.Endpoint);

            throw;
        }

        var sinkPayload = _mappingEngine.BuildSinkPayload(sourceObj, contract.Mappings);

        if (sinkPayload.Count == 0)
        {
            _logger.LogWarning("[{Contract}] No fields mapped. Skipping sink call.", contract.ContractId);
            return null;
        }

        return new ApiToApiExecutionResult
        {
            SourcePayload = sourceObj,
            SinkPayload = sinkPayload
        };
    }

    public async Task<JsonObject?> SendPayloadAsync(
        ApiToApiContract contract,
        JsonObject sinkPayload,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();

        try
        {
            using var req = new HttpRequestMessage(
                new HttpMethod(contract.Sink.Method ?? "POST"),
                contract.Sink.Endpoint)
            {
                Content = JsonContent.Create(sinkPayload)
            };

            using var resp = await client.SendAsync(req, cancellationToken);
            var responseText = await resp.Content.ReadAsStringAsync(cancellationToken);

            resp.EnsureSuccessStatusCode();

            JsonObject? responsePayload = null;

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                responsePayload = JsonNode.Parse(responseText) as JsonObject;
            }

            _logger.LogInformation(
                "[{Contract}] Forwarded payload to sink. Payload: {Payload}",
                contract.ContractId,
                sinkPayload.ToJsonString());

            return responsePayload;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{Contract}] Sink call failed: {Endpoint}",
                contract.ContractId,
                contract.Sink.Endpoint);

            throw;
        }
    }
}