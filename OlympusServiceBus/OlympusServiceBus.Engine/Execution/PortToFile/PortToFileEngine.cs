using System.Net;
using System.Text.Json.Nodes;
using OlympusServiceBus.Engine.Execution.Files;
using OlympusServiceBus.Engine.Execution.PortToApi;
using OlympusServiceBus.RuntimeState.Models;
using OlympusServiceBus.RuntimeState.Services;
using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.PortToFile;

public class PortToFileEngine : IPortToFileEngine
{
    private readonly ILogger<PortToFileEngine> _logger;
    private readonly IContractMessageStateService _messageStateService;
    private readonly PortToApiBusinessKeyProvider _businessKeyProvider;
    private readonly PortToApiPayloadHashProvider _payloadHashProvider;
    private readonly FileSinkService _fileSinkService;

    public PortToFileEngine(
        ILogger<PortToFileEngine> logger, 
        IContractMessageStateService messageStateService, 
        PortToApiBusinessKeyProvider businessKeyProvider, 
        PortToApiPayloadHashProvider payloadHashProvider, 
        FileSinkService fileSinkService)
    {
        _logger = logger;
        _messageStateService = messageStateService;
        _businessKeyProvider = businessKeyProvider;
        _payloadHashProvider = payloadHashProvider;
        _fileSinkService = fileSinkService;
    }

    public async Task<EngineResult> ExecuteAsync(
        PortToFileContract portToFileContract,
        JsonObject inbound,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(portToFileContract.Sink?.Directory))
        {
            return new EngineResult(
                Success: false,
                StatusCode: (int)HttpStatusCode.InternalServerError,
                Body: null,
                Error: "Sink directory missing");
        }

        var adapterContract = ToPortToApiContract(portToFileContract);

        var (outbound, mappingErrors) = PortToApiPayloadBuilder.BuildOutbound(adapterContract, inbound);

        if (mappingErrors.Count > 0)
        {
            return new EngineResult(
                Success: false,
                StatusCode: (int)HttpStatusCode.BadRequest,
                Body: new
                {
                    contractId = portToFileContract.ContractId,
                    mappingErrors
                },
                Error: "Transformation failed");
        }

        var businessKey = _businessKeyProvider.CreateKey(outbound, portToFileContract.BusinessKeyFields);
        var payloadHash = _payloadHashProvider.ComputeHash(outbound);

        var existingState = await _messageStateService.GetAsync(
            portToFileContract.ContractId,
            businessKey,
            cancellationToken);

        if (existingState is not null && existingState.PayloadHash == payloadHash)
        {
            existingState.LastSeenAt = DateTimeOffset.UtcNow;
            await _messageStateService.SaveAsync(existingState, cancellationToken);

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

        _logger.LogInformation(
            "[{Corr}] PortToFile {ContractId} -> {Directory}",
            context.CorrelationId,
            portToFileContract.ContractId,
            portToFileContract.Sink.Directory);

        _logger.LogInformation(
            "[{Corr}] Outbound payload: {Payload}",
            context.CorrelationId,
            outbound.ToJsonString());

        var attemptTime = DateTimeOffset.UtcNow;

        try
        {
            var writeResult = await _fileSinkService.WriteAsync(
                contract: portToFileContract,
                sink: portToFileContract.Sink,
                payload: outbound,
                mappings: portToFileContract.Mappings,
                cancellationToken: cancellationToken);

            var now = DateTimeOffset.UtcNow;

            if (existingState is null)
            {
                await _messageStateService.SaveAsync(new ContractMessageStateEntity
                {
                    ContractId = portToFileContract.ContractId,
                    ContractName = portToFileContract.Name,
                    BusinessKey = businessKey,
                    PayloadHash = payloadHash,
                    CanonicalSnapshot = outbound.ToJsonString(),
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    LastPublishedAt = now,
                    PublishStatus = "Published",
                    PublishAttemptCount = 1,
                    LastPublishAttemptAt = attemptTime,
                    LastPublishError = null
                }, cancellationToken);
            }
            else
            {
                existingState.PayloadHash = payloadHash;
                existingState.CanonicalSnapshot = outbound.ToJsonString();
                existingState.LastSeenAt = now;
                existingState.LastPublishedAt = now;
                existingState.PublishStatus = "Published";
                existingState.PublishAttemptCount += 1;
                existingState.LastPublishAttemptAt = attemptTime;
                existingState.LastPublishError = null;

                await _messageStateService.SaveAsync(existingState, cancellationToken);
            }

            return new EngineResult(
                Success: true,
                StatusCode: (int)HttpStatusCode.OK,
                Body: new
                {
                    written = true,
                    businessKey,
                    fileName = writeResult.FileName,
                    filePath = writeResult.FullPath,
                    outbound
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[{Corr}] PortToFile sink write failed for contract {ContractId}",
                context.CorrelationId,
                portToFileContract.ContractId);

            var now = DateTimeOffset.UtcNow;

            if (existingState is null)
            {
                await _messageStateService.SaveAsync(new ContractMessageStateEntity
                {
                    ContractId = portToFileContract.ContractId,
                    ContractName = portToFileContract.Name,
                    BusinessKey = businessKey,
                    PayloadHash = payloadHash,
                    CanonicalSnapshot = outbound.ToJsonString(),
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    LastPublishedAt = null,
                    PublishStatus = "Failed",
                    PublishAttemptCount = 1,
                    LastPublishAttemptAt = attemptTime,
                    LastPublishError = ex.Message
                }, cancellationToken);
            }
            else
            {
                existingState.PayloadHash = payloadHash;
                existingState.CanonicalSnapshot = outbound.ToJsonString();
                existingState.LastSeenAt = now;
                existingState.PublishStatus = "Failed";
                existingState.PublishAttemptCount += 1;
                existingState.LastPublishAttemptAt = attemptTime;
                existingState.LastPublishError = ex.Message;

                await _messageStateService.SaveAsync(existingState, cancellationToken);
            }

            return new EngineResult(
                Success: false,
                StatusCode: (int)HttpStatusCode.InternalServerError,
                Body: new
                {
                    written = false,
                    businessKey,
                    outbound
                },
                Error: ex.Message);
        }
    }

    private static PortToApiContract ToPortToApiContract(PortToFileContract contract)
    {
        return new PortToApiContract
        {
            ContractId = contract.ContractId,
            Name =  contract.Name,
            Enabled =  contract.Enabled,
            BusinessKeyFields =  contract.BusinessKeyFields,
            Request = contract.Request,
            Mappings =  contract.Mappings ?? Array.Empty<ApiFieldConfig>(),
            Sink = new ApiConfig(),
            Listener = contract.Listener
        };
    }
}