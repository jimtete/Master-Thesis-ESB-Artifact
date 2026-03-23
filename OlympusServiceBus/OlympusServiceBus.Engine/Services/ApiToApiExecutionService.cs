using System.Text.Json.Nodes;
using OlympusServiceBus.Engine.Execution.AntiContracts;
using OlympusServiceBus.Engine.Execution.ApiToApi;
using OlympusServiceBus.RuntimeState.Models;
using OlympusServiceBus.RuntimeState.Services;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Services;

public class ApiToApiExecutionService : IApiToApiExecutionService
{
    private readonly ApiToApiExecutor _executor;
    private readonly IContractExecutionStateService _executionStateService;
    private readonly IContractMessageStateService _messageStateService;
    private readonly ApiToApiBusinessKeyProvider _businessKeyProvider;
    private readonly ApiToApiPayloadHashProvider _payloadHashProvider;
    private readonly IAntiContractRegistry _antiContractRegistry;
    private readonly AntiContractDispatcher _antiContractDispatcher;

    public ApiToApiExecutionService(
        ApiToApiExecutor executor,
        IContractExecutionStateService executionStateService,
        IContractMessageStateService messageStateService,
        ApiToApiBusinessKeyProvider businessKeyProvider,
        ApiToApiPayloadHashProvider payloadHashProvider,
        IAntiContractRegistry antiContractRegistry,
        AntiContractDispatcher antiContractDispatcher)
    {
        _executor = executor;
        _executionStateService = executionStateService;
        _messageStateService = messageStateService;
        _businessKeyProvider = businessKeyProvider;
        _payloadHashProvider = payloadHashProvider;
        _antiContractRegistry = antiContractRegistry;
        _antiContractDispatcher = antiContractDispatcher;
    }

    public async Task ExecuteAsync(ApiToApiContract contract, CancellationToken cancellationToken)
    {
        await _executionStateService.SaveAsync(new ContractExecutionStateEntity
        {
            ContractId = contract.ContractId,
            ContractName = contract.Name,
            LastRunStartedAt = DateTimeOffset.UtcNow,
            LastRunStatus = "Running",
            UpdatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        try
        {
            var execution = await _executor.BuildExecutionAsync(contract, cancellationToken);

            if (execution is null || execution.SinkPayload is null)
            {
                await FlushPendingAsync(contract, cancellationToken);

                await _executionStateService.SaveAsync(new ContractExecutionStateEntity
                {
                    ContractId = contract.ContractId,
                    ContractName = contract.Name,
                    LastRunCompletedAt = DateTimeOffset.UtcNow,
                    LastRunStatus = "NoPayload",
                    UpdatedAt = DateTimeOffset.UtcNow
                }, cancellationToken);

                return;
            }

            var businessKey = _businessKeyProvider.CreateKey(
                execution.SinkPayload,
                contract.BusinessKeyFields);

            var payloadHash = _payloadHashProvider.ComputeHash(execution.SinkPayload);
            var now = DateTimeOffset.UtcNow;

            var existingState = await _messageStateService.GetAsync(
                contract.ContractId,
                businessKey,
                cancellationToken);

            if (existingState is null)
            {
                await _messageStateService.SaveAsync(new ContractMessageStateEntity
                {
                    ContractId = contract.ContractId,
                    ContractName = contract.Name,
                    BusinessKey = businessKey,
                    PayloadHash = payloadHash,
                    CanonicalSnapshot = execution.SinkPayload.ToJsonString(),
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    PublishStatus = "Pending",
                    PublishAttemptCount = 0,
                    LastPublishAttemptAt = null,
                    LastPublishError = null,
                    LastPublishedAt = null
                }, cancellationToken);
            }
            else if (existingState.PayloadHash == payloadHash)
            {
                existingState.LastSeenAt = now;
                await _messageStateService.SaveAsync(existingState, cancellationToken);
            }
            else
            {
                existingState.PayloadHash = payloadHash;
                existingState.CanonicalSnapshot = execution.SinkPayload.ToJsonString();
                existingState.LastSeenAt = now;
                existingState.LastPublishedAt = null;
                existingState.PublishStatus = "Pending";
                existingState.PublishAttemptCount = 0;
                existingState.LastPublishAttemptAt = null;
                existingState.LastPublishError = null;

                await _messageStateService.SaveAsync(existingState, cancellationToken);
            }

            await FlushPendingAsync(contract, cancellationToken);

            await _executionStateService.SaveAsync(new ContractExecutionStateEntity
            {
                ContractId = contract.ContractId,
                ContractName = contract.Name,
                LastRunCompletedAt = DateTimeOffset.UtcNow,
                LastRunStatus = "Completed",
                UpdatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);
        }
        catch
        {
            await _executionStateService.SaveAsync(new ContractExecutionStateEntity
            {
                ContractId = contract.ContractId,
                ContractName = contract.Name,
                LastRunCompletedAt = DateTimeOffset.UtcNow,
                LastRunStatus = "Failed",
                UpdatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            throw;
        }
    }

    public async Task FlushPendingAsync(ApiToApiContract contract, CancellationToken cancellationToken)
    {
        var pendingMessages = await _messageStateService.GetPendingAsync(
            contract.ContractId,
            cancellationToken);

        foreach (var message in pendingMessages)
        {
            JsonObject? transformedPayload = null;
            JsonObject? responsePayload = null;

            try
            {
                if (string.IsNullOrWhiteSpace(message.CanonicalSnapshot))
                    continue;

                transformedPayload = JsonNode.Parse(message.CanonicalSnapshot) as JsonObject;
                if (transformedPayload is null)
                    continue;

                message.PublishAttemptCount += 1;
                message.LastPublishAttemptAt = DateTimeOffset.UtcNow;
                message.LastPublishError = null;

                responsePayload = await _executor.SendPayloadAsync(
                    contract,
                    transformedPayload,
                    cancellationToken);

                var now = DateTimeOffset.UtcNow;

                message.LastPublishedAt = now;
                message.LastSeenAt = now;
                message.PublishStatus = "Published";
                message.LastPublishError = null;

                await _messageStateService.SaveAsync(message, cancellationToken);

                await DispatchAntiContractsAsync(
                    contract,
                    message.BusinessKey,
                    null,
                    transformedPayload,
                    responsePayload,
                    "Success",
                    null,
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                message.PublishStatus = "Failed";
                message.LastPublishError = ex.Message;

                await _messageStateService.SaveAsync(message, cancellationToken);

                await DispatchAntiContractsAsync(
                    contract,
                    message.BusinessKey,
                    null,
                    transformedPayload,
                    responsePayload,
                    "Failed",
                    ex.Message,
                    "SINK_HTTP_FAILURE",
                    cancellationToken);
            }
        }
    }

    private async Task DispatchAntiContractsAsync(
        ContractBase contract,
        string businessKey,
        JsonObject? originalPayload,
        JsonObject? transformedPayload,
        JsonObject? responsePayload,
        string executionStatus,
        string? errorMessage,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        var antiContracts = _antiContractRegistry.GetBySourceContractId(contract.ContractId);
        if (antiContracts.Count == 0)
            return;

        var correlationValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var antiContract in antiContracts)
        {
            var values = AntiContractCorrelationValueFactory.Create(
                antiContract,
                originalPayload,
                transformedPayload,
                responsePayload);

            foreach (var pair in values)
            {
                correlationValues[pair.Key] = pair.Value;
            }
        }

        var context = string.Equals(executionStatus, "Success", StringComparison.OrdinalIgnoreCase)
            ? AntiContractExecutionContextFactory.CreateSuccess(
                contract,
                businessKey,
                originalPayload,
                transformedPayload,
                responsePayload,
                correlationValues)
            : AntiContractExecutionContextFactory.CreateFailure(
                contract,
                businessKey,
                errorMessage,
                errorCode,
                originalPayload,
                transformedPayload,
                responsePayload,
                correlationValues);

        await _antiContractDispatcher.DispatchAsync(
            contract.ContractId,
            context,
            cancellationToken);
    }
}