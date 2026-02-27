using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OlympusServiceBus.Utils.Contracts;
using Constants = OlympusServiceBus.Utils.Constants;

namespace OlympusServiceBus.Engine.Execution.PortToApi;

public sealed class PortToApiEngine : IPortToApiEngine
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PortToApiEngine> _logger;

    public PortToApiEngine(IHttpClientFactory httpClientFactory, ILogger<PortToApiEngine> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<EngineResult> ExecuteAsync(
        PortToApiContract portToApiContract,
        JsonObject inbound,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        var sinkUrl = portToApiContract.Sink?.Endpoint;
        var sinkMethod = (portToApiContract.Sink?.Method ?? "POST").Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(sinkUrl))
            return new EngineResult(false, (int)HttpStatusCode.BadGateway, null, "Sink endpoint missing");

        // 1) Transform inbound -> outbound using contract mappings (Type1)
        var (outbound, mappingErrors) = PortToApiPayloadBuilder.BuildOutbound(portToApiContract, inbound);

        if (mappingErrors.Count > 0)
        {
            return new EngineResult(
                Success: false,
                StatusCode: (int)HttpStatusCode.BadRequest,
                Body: new
                {
                    contractId = portToApiContract.ContractId,
                    mappingErrors
                },
                Error: "Transformation failed");
        }

        _logger.LogInformation("[{Corr}] PortToApi {ContractId} -> {Method} {Sink}",
            context.CorrelationId, portToApiContract.ContractId, sinkMethod, sinkUrl);

        _logger.LogInformation("[{Corr}] Outbound payload: {Payload}",
            context.CorrelationId, outbound.ToJsonString());

        // 2) Post outbound to sink
        var client = _httpClientFactory.CreateClient(Constants.ENGINE_HTTP_CLIENT_NAME);

        using var req = new HttpRequestMessage(new HttpMethod(sinkMethod), sinkUrl)
        {
            Content = JsonContent.Create(outbound)
        };

        if (!string.IsNullOrWhiteSpace(context.CorrelationId))
            req.Headers.TryAddWithoutValidation("X-Correlation-Id", context.CorrelationId);

        using var response = await client.SendAsync(req, cancellationToken);

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        // Try parse sink response as JSON for nicer output
        object sinkBody = responseText;
        try
        {
            var parsed = JsonNode.Parse(responseText);
            if (parsed is not null) sinkBody = parsed;
        }
        catch
        {
            // keep as string if not JSON
        }

        if (response.IsSuccessStatusCode)
        {
            return new EngineResult(
                Success: true,
                StatusCode: (int)response.StatusCode,
                Body: new
                {
                    forwarded = true,
                    sinkStatus = (int)response.StatusCode,
                    outbound,     // helpful for debugging PoC; remove later if you want
                    sinkBody
                });
        }

        return new EngineResult(
            Success: false,
            StatusCode: (int)response.StatusCode,
            Body: new
            {
                forwarded = false,
                sinkStatus = (int)response.StatusCode,
                outbound,     // helpful for debugging PoC; remove later if you want
                sinkBody
            },
            Error: $"Sink returned {(int)response.StatusCode}");
    }
}