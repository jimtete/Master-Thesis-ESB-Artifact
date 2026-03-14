using System.Text.Json.Nodes;
using OlympusServiceBus.Engine.Execution.AntiContracts;
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
        JsonObject? sourcePayload = null;
        JsonObject? transformedPayload = null;
        JsonObject? responsePayload = null;
        string businessKey = string.Empty;

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

            sourcePayload = execution.SourcePayload;
            transformedPayload = execution.SinkPayload;

            businessKey = _businessKeyProvider.CreateKey(transformedPayload, contract.BusinessKeyFields);
            var payloadHash = _payloadHashProvider.ComputeHash(transformedPayload);

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

            responsePayload = await _executor.SendPayloadAsync(
                contract,
                transformedPayload,
                cancellationToken);

            var now = DateTimeOffset.UtcNow;

            await _messageStateService.SaveAsync(new ContractMessageStateEntity
            {
                ContractId = contract.ContractId,
                ContractName = contract.Name,
                BusinessKey = businessKey,
                PayloadHash = payloadHash,
                CanonicalSnapshot = transformedPayload.ToJsonString(),
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

            await DispatchAntiContractsAsync(
                contract,
                businessKey,
                sourcePayload,
                transformedPayload,
                responsePayload,
                executionStatus: "Success",
                errorMessage: null,
                errorCode: null,
                cancellationToken);
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

            await DispatchAntiContractsAsync(
                contract,
                businessKey,
                sourcePayload,
                transformedPayload,
                responsePayload,
                executionStatus: "Failed",
                errorMessage: ex.Message,
                errorCode: "SINK_HTTP_FAILURE",
                cancellationToken);

            throw;
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