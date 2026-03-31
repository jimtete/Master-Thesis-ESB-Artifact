using OlympusServiceBus.Engine.Execution.ApiToApi;
using OlympusServiceBus.Engine.Execution.ApiToFile;
using OlympusServiceBus.RuntimeState.Models;
using OlympusServiceBus.RuntimeState.Services;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Services;

public sealed class ApiToFileExecutionService : IApiToFileExecutionService
{
    private readonly ApiToFileExecutor _executor;
    private readonly IContractExecutionStateService _executionStateService;
    private readonly IContractMessageStateService _messageStateService;
    private readonly ApiToApiBusinessKeyProvider _businessKeyProvider;
    private readonly ApiToApiPayloadHashProvider _payloadHashProvider;

    public ApiToFileExecutionService(
        ApiToFileExecutor executor, 
        IContractExecutionStateService executionStateService, 
        IContractMessageStateService messageStateService, 
        ApiToApiBusinessKeyProvider businessKeyProvider, 
        ApiToApiPayloadHashProvider payloadHashProvider)
    {
        _executor = executor;
        _executionStateService = executionStateService;
        _messageStateService = messageStateService;
        _businessKeyProvider = businessKeyProvider;
        _payloadHashProvider = payloadHashProvider;
    }
    
    public async Task ExecuteAsync(ApiToFileContract contract, CancellationToken cancellationToken)
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

            if (existingState is not null && existingState.PayloadHash == payloadHash)
            {
                existingState.LastSeenAt = now;

                await _messageStateService.SaveAsync(existingState, cancellationToken);

                await _executionStateService.SaveAsync(new ContractExecutionStateEntity
                {
                    ContractId = contract.ContractId,
                    ContractName = contract.Name,
                    LastRunCompletedAt = now,
                    LastRunStatus = "DuplicateSkipped",
                    UpdatedAt = now
                }, cancellationToken);

                return;
            }

            var writeResult = await _executor.WritePayloadAsync(
                contract,
                execution.SinkPayload,
                cancellationToken);

            var publishedAt = DateTimeOffset.UtcNow;

            await _messageStateService.SaveAsync(new ContractMessageStateEntity
            {
                ContractId = contract.ContractId,
                ContractName = contract.Name,
                BusinessKey = businessKey,
                PayloadHash = payloadHash,
                CanonicalSnapshot = execution.SinkPayload.ToJsonString(),
                FirstSeenAt = existingState?.FirstSeenAt ?? now,
                LastSeenAt = publishedAt,
                LastPublishedAt = publishedAt,
                PublishStatus = "Published",
                PublishAttemptCount = (existingState?.PublishAttemptCount ?? 0) + 1,
                LastPublishAttemptAt = publishedAt,
                LastPublishError = null
            }, cancellationToken);

            await _executionStateService.SaveAsync(new ContractExecutionStateEntity
            {
                ContractId = contract.ContractId,
                ContractName = contract.Name,
                LastRunCompletedAt = publishedAt,
                LastRunStatus = "Completed",
                UpdatedAt = publishedAt
            }, cancellationToken);
        }
        catch (Exception ex)
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
}