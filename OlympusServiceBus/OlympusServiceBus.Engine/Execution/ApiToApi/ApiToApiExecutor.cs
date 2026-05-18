using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OlympusServiceBus.Engine.Evaluation;
using OlympusServiceBus.Engine.Execution.Transformation;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.ApiToApi;

public class ApiToApiExecutor
{
    private readonly ILogger<ApiToApiExecutor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMappingEngine _mappingEngine;
    private readonly IEvaluationVerboseLogger _evaluationVerboseLogger;

    public ApiToApiExecutor(
        ILogger<ApiToApiExecutor> logger,
        IHttpClientFactory httpClientFactory,
        IMappingEngine mappingEngine,
        IEvaluationVerboseLogger evaluationVerboseLogger)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _mappingEngine = mappingEngine;
        _evaluationVerboseLogger = evaluationVerboseLogger;
    }

    public async Task<ApiToApiExecutionResult?> BuildExecutionAsync(
        ApiToApiContract contract,
        string correlationId,
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
        var sourceFailureLogged = false;
        try
        {
            _evaluationVerboseLogger.LogApiSourceRequest(
                contract,
                correlationId,
                contract.Source.Method,
                contract.Source.Endpoint);

            using var resp = await client.GetAsync(contract.Source.Endpoint, cancellationToken);
            var text = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                var exception = new HttpRequestException(
                    $"Source returned HTTP {(int)resp.StatusCode} ({resp.ReasonPhrase}).",
                    null,
                    resp.StatusCode);

                _evaluationVerboseLogger.LogApiSourceFailure(
                    contract,
                    correlationId,
                    contract.Source.Method,
                    contract.Source.Endpoint,
                    exception,
                    (int)resp.StatusCode,
                    text);
                sourceFailureLogged = true;

                throw exception;
            }

            _evaluationVerboseLogger.LogApiSourceResponse(
                contract,
                correlationId,
                contract.Source.Method,
                contract.Source.Endpoint,
                (int)resp.StatusCode,
                text);

            sourceObj = JsonNode.Parse(text) as JsonObject;

            if (sourceObj is null)
            {
                _logger.LogWarning("[{Contract}] Source returned non-object JSON.", contract.ContractId);
                return null;
            }
        }
        catch (Exception ex)
        {
            if (!sourceFailureLogged)
            {
                _evaluationVerboseLogger.LogApiSourceFailure(
                    contract,
                    correlationId,
                    contract.Source.Method,
                    contract.Source.Endpoint,
                    ex);
            }

            _logger.LogError(
                ex,
                "[{Contract}] Source call failed: {Endpoint}",
                contract.ContractId,
                contract.Source.Endpoint);

            throw;
        }

        var sinkPayload = _mappingEngine.BuildSinkPayload(
            sourceObj,
            contract.Mappings,
            contract.ContractId,
            correlationId);

        _evaluationVerboseLogger.LogTransformation(
            contract,
            correlationId,
            contract.Mappings,
            sourceObj,
            sinkPayload,
            sourceLabel: "source-response");

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
        string correlationId,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var method = contract.Sink.Method ?? "POST";
        var sinkFailureLogged = false;

        try
        {
            _evaluationVerboseLogger.LogApiSinkRequest(
                contract,
                correlationId,
                method,
                contract.Sink.Endpoint,
                sinkPayload);

            using var req = new HttpRequestMessage(
                new HttpMethod(method),
                contract.Sink.Endpoint)
            {
                Content = JsonContent.Create(sinkPayload)
            };

            using var resp = await client.SendAsync(req, cancellationToken);
            var responseText = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                var exception = new HttpRequestException(
                    $"Sink returned HTTP {(int)resp.StatusCode} ({resp.ReasonPhrase}).",
                    null,
                    resp.StatusCode);

                _evaluationVerboseLogger.LogApiSinkFailure(
                    contract,
                    correlationId,
                    method,
                    contract.Sink.Endpoint,
                    sinkPayload,
                    exception,
                    (int)resp.StatusCode,
                    responseText);
                sinkFailureLogged = true;

                throw exception;
            }

            JsonObject? responsePayload = null;

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                responsePayload = JsonNode.Parse(responseText) as JsonObject;
            }

            _evaluationVerboseLogger.LogApiSinkResponse(
                contract,
                correlationId,
                method,
                contract.Sink.Endpoint,
                (int)resp.StatusCode,
                responseText,
                sinkPayload);

            _logger.LogInformation(
                "[{Contract}] Forwarded payload to sink. Payload: {Payload}",
                contract.ContractId,
                sinkPayload.ToJsonString());

            return responsePayload;
        }
        catch (Exception ex)
        {
            if (!sinkFailureLogged)
            {
                _evaluationVerboseLogger.LogApiSinkFailure(
                    contract,
                    correlationId,
                    method,
                    contract.Sink.Endpoint,
                    sinkPayload,
                    ex);
            }

            _logger.LogError(
                ex,
                "[{Contract}] Sink call failed: {Endpoint}",
                contract.ContractId,
                contract.Sink.Endpoint);

            throw;
        }
    }
}
