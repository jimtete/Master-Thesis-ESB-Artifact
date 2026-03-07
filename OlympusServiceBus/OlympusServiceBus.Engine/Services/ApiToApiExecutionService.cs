using OlympusServiceBus.Engine.Execution.ApiToApi;
using OlympusServiceBus.Engine.Helpers;
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

    public ApiToApiExecutionService(
        ApiToApiExecutor executor,
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

        var payload = await _executor.BuildPayloadAsync(contract, cancellationToken);

        if (payload is null)
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

        var businessKey = _businessKeyProvider.CreateKey(payload, contract.BusinessKeyFields);
        var payloadHash = _payloadHashProvider.ComputeHash(payload);

        var existingState = await _messageStateService.GetAsync(
            contract.ContractId,
            businessKey,
            cancellationToken);

        if (existingState is not null && existingState.PayloadHash == payloadHash)
        {
            existingState.LastSeenAt = DateTimeOffset.UtcNow;

            await _messageStateService.SaveAsync(existingState, cancellationToken);

            await _executionStateService.SaveAsync(new ContractExecutionStateEntity
            {
                ContractId = contract.ContractId,
                ContractName = contract.Name,
                LastRunCompletedAt = DateTimeOffset.UtcNow,
                LastRunStatus = "DuplicateSkipped",
                UpdatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            return;
        }

        await _executor.SendPayloadAsync(contract, payload, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        await _messageStateService.SaveAsync(new ContractMessageStateEntity
        {
            ContractId = contract.ContractId,
            ContractName = contract.Name,
            BusinessKey = businessKey,
            PayloadHash = payloadHash,
            CanonicalSnapshot = payload.ToJsonString(),
            FirstSeenAt = existingState?.FirstSeenAt ?? now,
            LastSeenAt = now,
            LastPublishedAt = now
        }, cancellationToken);

        await _executionStateService.SaveAsync(new ContractExecutionStateEntity
        {
            ContractId = contract.ContractId,
            ContractName = contract.Name,
            LastRunCompletedAt = now,
            LastRunStatus = "Completed",
            UpdatedAt = now
        }, cancellationToken);
    }
}