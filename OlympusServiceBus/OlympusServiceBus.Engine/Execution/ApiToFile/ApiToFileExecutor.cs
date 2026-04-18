using System.Text.Json.Nodes;
using OlympusServiceBus.Engine.Execution.Files;
using OlympusServiceBus.Engine.Execution.Transformation;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.ApiToFile;

public class ApiToFileExecutor
{
    private readonly ILogger<ApiToFileExecutor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FileSinkService _fileSinkService;
    private readonly IMappingEngine _mappingEngine;

    public ApiToFileExecutor(
        ILogger<ApiToFileExecutor> logger,
        IHttpClientFactory httpClientFactory,
        FileSinkService fileSinkService,
        IMappingEngine mappingEngine)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _fileSinkService = fileSinkService;
        _mappingEngine = mappingEngine;
    }

    public async Task<ApiToFileExecutionResult?> BuildExecutionAsync(
        ApiToFileContract contract,
        CancellationToken cancellationToken)
    {
        if (!contract.Enabled)
            return null;

        if (string.IsNullOrWhiteSpace(contract.Source?.Endpoint))
        {
            _logger.LogWarning("[{Contract}] Missing Source.Endpoint.", contract.ContractId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(contract.Sink?.Directory))
        {
            _logger.LogWarning("[{Contract}] Missing Sink.Directory.", contract.ContractId);
            return null;
        }

        contract.Source.Method ??= "GET";

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
            _logger.LogWarning("[{Contract}] No fields mapped. Skipping sink write.", contract.ContractId);
            return null;
        }

        return new ApiToFileExecutionResult
        {
            SourcePayload = sourceObj,
            SinkPayload = sinkPayload
        };
    }

    public Task<FileWriteResult> WritePayloadAsync(
        ApiToFileContract contract,
        JsonObject sinkPayload,
        CancellationToken cancellationToken)
    {
        return _fileSinkService.WriteAsync(
            contract,
            contract.Sink,
            sinkPayload,
            contract.Mappings,
            cancellationToken);
    }
}

public sealed class ApiToFileExecutionResult
{
    public JsonObject? SourcePayload { get; set; }
    public JsonObject? SinkPayload { get; set; }
}