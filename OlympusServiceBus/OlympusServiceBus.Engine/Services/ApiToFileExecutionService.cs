using System.Diagnostics;
using OlympusServiceBus.Engine.Evaluation;
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
    private readonly IEvaluationRecordingService _evaluationRecordingService;

    public ApiToFileExecutionService(
        ApiToFileExecutor executor, 
        IContractExecutionStateService executionStateService, 
        IContractMessageStateService messageStateService, 
        ApiToApiBusinessKeyProvider businessKeyProvider, 
        ApiToApiPayloadHashProvider payloadHashProvider,
        IEvaluationRecordingService evaluationRecordingService)
    {
        _executor = executor;
        _executionStateService = executionStateService;
        _messageStateService = messageStateService;
        _businessKeyProvider = businessKeyProvider;
        _payloadHashProvider = payloadHashProvider;
        _evaluationRecordingService = evaluationRecordingService;
    }
    
    public async Task ExecuteAsync(ApiToFileContract contract, string triggerType, CancellationToken cancellationToken)
    {
        var activeSession = await _evaluationRecordingService.GetActiveSessionAsync(cancellationToken);
        var startedAtUtc = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var status = "Success";
        string? errorMessage = null;
        var processedCount = 0;

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
                status = "NoPayload";

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
                status = "DuplicateSkipped";

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
            processedCount = 1;

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
            status = "Failed";
            errorMessage = ex.Message;

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
        finally
        {
            stopwatch.Stop();

            if (activeSession is not null)
            {
                var metadata = EvaluationContractMetadataResolver.Resolve(contract);
                await _evaluationRecordingService.RecordJobAsync(new EvaluationJobRecord
                {
                    RecordingSessionId = activeSession.SessionId,
                    ContractId = contract.ContractId,
                    ContractName = contract.Name,
                    ContractType = metadata.ContractType,
                    ScheduleMode = metadata.ScheduleMode,
                    TriggerType = triggerType,
                    SourceType = metadata.SourceType,
                    SinkType = metadata.SinkType,
                    StartTimestampUtc = startedAtUtc,
                    EndTimestampUtc = DateTimeOffset.UtcNow,
                    DurationMilliseconds = stopwatch.ElapsedMilliseconds,
                    Status = status,
                    ErrorMessage = errorMessage,
                    ProcessedRowsOrMessagesCount = processedCount
                }, cancellationToken);
            }
        }
    }
}
