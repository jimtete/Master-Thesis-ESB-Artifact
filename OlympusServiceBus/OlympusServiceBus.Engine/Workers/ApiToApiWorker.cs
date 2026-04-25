using OlympusServiceBus.Engine.Helpers;
using OlympusServiceBus.Engine.Scheduling;
using OlympusServiceBus.Engine.Services;
using OlympusServiceBus.RuntimeState.Services;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Workers;

public class ApiToApiWorker(
    ILogger<ApiToApiWorker> logger,
    IContractRegistry registry,
    IServiceScopeFactory scopeFactory
) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ApiToApiWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var contracts = registry.GetContract<ApiToApiContract>();

            foreach (var contract in contracts)
            {
                if (!contract.Enabled)
                    continue;

                try
                {
                    using var scope = scopeFactory.CreateScope();

                    var executionStateService = scope.ServiceProvider.GetRequiredService<IContractExecutionStateService>();
                    var executionService = scope.ServiceProvider.GetRequiredService<IApiToApiExecutionService>();

                    var resolvedSchedule = ContractScheduleResolver.Resolve(contract);
                    var executionState = await executionStateService.GetAsync(contract.ContractId, stoppingToken);

                    var nowUtc = DateTimeOffset.UtcNow;
                    var isDue = ContractScheduleDueEvaluator.IsDue(
                        resolvedSchedule,
                        executionState,
                        nowUtc);

                    logger.LogDebug(
                        "Contract {ContractId} schedule evaluated. Mode: {Mode}, IsDue: {IsDue}, LastRunStartedAt: {LastRunStartedAt}, LastRunCompletedAt: {LastRunCompletedAt}, LastRunStatus: {LastRunStatus}",
                        contract.ContractId,
                        resolvedSchedule.Mode,
                        isDue,
                        executionState?.LastRunStartedAt,
                        executionState?.LastRunCompletedAt,
                        executionState?.LastRunStatus);

                    if (!isDue)
                        continue;

                    logger.LogInformation(
                        "Contract {ContractId} is due for execution. Schedule mode: {Mode}",
                        contract.ContractId,
                        resolvedSchedule.Mode);

                    await executionService.ExecuteAsync(contract, stoppingToken);
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
                        "ApiToApiWorker failed while evaluating or executing contract {ContractId}",
                        contract.ContractId);
                }
            }

            await Task.Delay(Tick, stoppingToken);
        }
    }
}
