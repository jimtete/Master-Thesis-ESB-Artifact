using OlympusServiceBus.Engine.Evaluation;
using OlympusServiceBus.Engine.Helpers;
using OlympusServiceBus.Engine.Scheduling;
using OlympusServiceBus.Engine.Services;
using OlympusServiceBus.RuntimeState.Services;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Workers;

public sealed class ApiToFileWorker(
    ILogger<ApiToFileWorker> logger,
    IContractRegistry registry,
    IServiceScopeFactory scopeFactory
) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ApiToFileWorker started.");
        var activationTracker = new ContractActivationTracker();

        while (!stoppingToken.IsCancellationRequested)
        {
            var contracts = registry.GetContract<ApiToFileContract>();
            activationTracker.SyncKnownContracts(contracts.Select(static contract => contract.ContractId));

            foreach (var contract in contracts)
            {
                try
                {
                    var nowUtc = DateTimeOffset.UtcNow;
                    var activationStartedAtUtc = activationTracker.Observe(contract.ContractId, contract.Enabled, nowUtc);

                    if (!contract.Enabled)
                        continue;

                    using var scope = scopeFactory.CreateScope();

                    var executionStateService = scope.ServiceProvider.GetRequiredService<IContractExecutionStateService>();
                    var executionService = scope.ServiceProvider.GetRequiredService<IApiToFileExecutionService>();

                    var resolvedSchedule = ContractScheduleResolver.Resolve(contract);
                    var executionState = await executionStateService.GetAsync(contract.ContractId, stoppingToken);

                    var isDue = ContractScheduleDueEvaluator.IsDue(
                        resolvedSchedule,
                        executionState,
                        nowUtc,
                        activationStartedAtUtc);

                    logger.LogDebug(
                        "Contract {ContractId} schedule evaluated. Mode: {Mode}, IsDue: {IsDue}, ActivationStartedAt: {ActivationStartedAt}, LastRunStartedAt: {LastRunStartedAt}, LastRunCompletedAt: {LastRunCompletedAt}, LastRunStatus: {LastRunStatus}",
                        contract.ContractId,
                        resolvedSchedule.Mode,
                        isDue,
                        activationStartedAtUtc,
                        executionState?.LastRunStartedAt,
                        executionState?.LastRunCompletedAt,
                        executionState?.LastRunStatus);

                    if (!isDue)
                        continue;

                    logger.LogInformation(
                        "Contract {ContractId} is due for execution. Schedule mode: {Mode}",
                        contract.ContractId,
                        resolvedSchedule.Mode);

                    await executionService.ExecuteAsync(contract, EvaluationTriggerTypes.Scheduled, stoppingToken);
                    activationTracker.MarkExecuted(contract.ContractId);
                }
                catch (NotSupportedException ex)
                {
                    logger.LogWarning(
                        ex,
                        "Skipping contract {ContractId} because its schedule mode is not yet supported.",
                        contract.ContractId);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "ApiToFileWorker failed while evaluating or executing contract {ContractId}",
                        contract.ContractId);
                }
            }

            await Task.Delay(Tick, stoppingToken);
        }
    }
}
