using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OlympusServiceBus.Engine.Evaluation;
using OlympusServiceBus.Engine.Helpers;
using OlympusServiceBus.RuntimeState.Models;
using OlympusServiceBus.RuntimeState.Services;
using OlympusServiceBus.Utils.Contracts;
using Constants = OlympusServiceBus.Utils.Constants;

namespace OlympusServiceBus.Engine.Execution.PortToApi;

public sealed class PortToApiEngine : IPortToApiEngine
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PortToApiEngine> _logger;
    private readonly IContractMessageStateService _messageStateService;
    private readonly PortToApiBusinessKeyProvider _businessKeyProvider;
    private readonly PortToApiPayloadHashProvider _payloadHashProvider;
    private readonly IEvaluationVerboseLogger _evaluationVerboseLogger;

    public PortToApiEngine(
        IHttpClientFactory httpClientFactory, 
        ILogger<PortToApiEngine> logger,
        IContractMessageStateService messageStateService,
        PortToApiBusinessKeyProvider businessKeyProvider,
        PortToApiPayloadHashProvider payloadHashProvider,
        IEvaluationVerboseLogger evaluationVerboseLogger
    )
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _messageStateService = messageStateService;
        _businessKeyProvider = businessKeyProvider;
        _payloadHashProvider = payloadHashProvider;
        _evaluationVerboseLogger = evaluationVerboseLogger;
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
        _evaluationVerboseLogger.LogTransformation(
            portToApiContract,
            context.CorrelationId,
            portToApiContract.Mappings,
            inbound,
            outbound,
            mappingErrors,
            sourceLabel: "inbound-request");

        if (mappingErrors.Count > 0)
        {
            _logger.LogWarning(
                "[{Corr}] Transformation failed for contract {ContractId}. MappingErrors: {MappingErrors}",
                context.CorrelationId,
                portToApiContract.ContractId,
                string.Join(" | ", mappingErrors));

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
        
        var businessKey = _businessKeyProvider.CreateKey(outbound, portToApiContract.BusinessKeyFields);
        var payloadHash = _payloadHashProvider.ComputeHash(outbound);

        var existingState = await _messageStateService.GetAsync(
            portToApiContract.ContractId,
            businessKey,
            cancellationToken);

        if (existingState is not null && existingState.PayloadHash == payloadHash)
        {
            existingState.LastSeenAt = DateTimeOffset.UtcNow;
            await _messageStateService.SaveAsync(existingState, cancellationToken);
            _evaluationVerboseLogger.LogRuntimeState(
                portToApiContract,
                context.CorrelationId,
                existingState,
                duplicateSkipped: true,
                note: "Duplicate inbound payload skipped.");

            return new EngineResult(
                Success: true,
                StatusCode: (int)HttpStatusCode.OK,
                Body: new
                {
                    skipped = true,
                    reason = "Duplicate payload",
                    businessKey
                });
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

        _evaluationVerboseLogger.LogApiSinkRequest(
            portToApiContract,
            context.CorrelationId,
            sinkMethod,
            sinkUrl,
            outbound);

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
            var now = DateTimeOffset.UtcNow;
            var publishedState = new ContractMessageStateEntity
            {
                ContractId = portToApiContract.ContractId,
                ContractName = portToApiContract.Name,
                BusinessKey = businessKey,
                PayloadHash = payloadHash,
                CanonicalSnapshot = outbound.ToJsonString(),
                FirstSeenAt = existingState?.FirstSeenAt ?? now,
                LastSeenAt = now,
                LastPublishedAt = now,
                PublishStatus = "Published",
                PublishAttemptCount = (existingState?.PublishAttemptCount ?? 0) + 1,
                LastPublishAttemptAt = now,
                LastPublishError = null
            };

            await _messageStateService.SaveAsync(publishedState, cancellationToken);
            _evaluationVerboseLogger.LogApiSinkResponse(
                portToApiContract,
                context.CorrelationId,
                sinkMethod,
                sinkUrl,
                (int)response.StatusCode,
                responseText,
                outbound);
            _evaluationVerboseLogger.LogRuntimeState(
                portToApiContract,
                context.CorrelationId,
                publishedState,
                note: "Sink publish succeeded.");
            
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

        var failedAt = DateTimeOffset.UtcNow;
        var failedState = new ContractMessageStateEntity
        {
            ContractId = portToApiContract.ContractId,
            ContractName = portToApiContract.Name,
            BusinessKey = businessKey,
            PayloadHash = payloadHash,
            CanonicalSnapshot = outbound.ToJsonString(),
            FirstSeenAt = existingState?.FirstSeenAt ?? failedAt,
            LastSeenAt = failedAt,
            LastPublishedAt = existingState?.LastPublishedAt,
            PublishStatus = "Failed",
            PublishAttemptCount = (existingState?.PublishAttemptCount ?? 0) + 1,
            LastPublishAttemptAt = failedAt,
            LastPublishError = $"Sink returned {(int)response.StatusCode} ({response.ReasonPhrase})."
        };

        await _messageStateService.SaveAsync(failedState, cancellationToken);
        _evaluationVerboseLogger.LogApiSinkFailure(
            portToApiContract,
            context.CorrelationId,
            sinkMethod,
            sinkUrl,
            outbound,
            new HttpRequestException(
                $"Sink returned {(int)response.StatusCode} ({response.ReasonPhrase}).",
                null,
                response.StatusCode),
            (int)response.StatusCode,
            responseText);
        _evaluationVerboseLogger.LogRuntimeState(
            portToApiContract,
            context.CorrelationId,
            failedState,
            note: "Sink publish failed.");

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
