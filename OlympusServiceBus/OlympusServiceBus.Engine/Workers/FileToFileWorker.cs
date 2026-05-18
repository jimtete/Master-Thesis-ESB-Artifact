using OlympusServiceBus.Engine.Evaluation;
using OlympusServiceBus.Engine.Execution.FileToFile;
using OlympusServiceBus.Engine.Helpers;
using OlympusServiceBus.Engine.Scheduling;
using OlympusServiceBus.RuntimeState.Models;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Workers;

public sealed class FileToFileWorker
(
    ILogger<FileToFileWorker> logger,
    IContractRegistry registry,
    IServiceScopeFactory scopeFactory
) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("FileToFileWorker started.");

        var lastRun = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        var activationTracker = new ContractActivationTracker();

        while (!stoppingToken.IsCancellationRequested)
        {
            var contracts = registry.GetContract<FileToFileContract>();
            SyncLastRun(lastRun, contracts);
            activationTracker.SyncKnownContracts(contracts.Select(static contract => contract.ContractId));

            foreach (var contract in contracts)
            {
                var nowUtc = DateTimeOffset.UtcNow;
                var activationStartedAtUtc = activationTracker.Observe(contract.ContractId, contract.Enabled, nowUtc);

                if (!contract.Enabled)
                    continue;

                var resolvedSchedule = ContractScheduleResolver.Resolve(contract);
                ContractExecutionStateEntity? executionState = null;
                var lastRunStartedAt = lastRun[contract.ContractId];

                if (lastRunStartedAt != DateTimeOffset.MinValue)
                {
                    executionState = new ContractExecutionStateEntity
                    {
                        ContractId = contract.ContractId,
                        ContractName = contract.Name,
                        LastRunStartedAt = lastRunStartedAt
                    };
                }

                var isDue = ContractScheduleDueEvaluator.IsDue(
                    resolvedSchedule,
                    executionState,
                    nowUtc,
                    activationStartedAtUtc);

                if (!isDue)
                {
                    continue;
                }

                using var scope = scopeFactory.CreateScope();
                var executor = scope.ServiceProvider.GetRequiredService<FileToFileExecutor>();

                await executor.ExecuteOnce(contract, EvaluationTriggerTypes.FilePolling, stoppingToken);
                lastRun[contract.ContractId] = DateTimeOffset.UtcNow;
                activationTracker.MarkExecuted(contract.ContractId);
            }

            await Task.Delay(Tick, stoppingToken);
        }
    }

    private static void SyncLastRun(
        IDictionary<string, DateTimeOffset> lastRun,
        IReadOnlyList<FileToFileContract> contracts)
    {
        var activeContractIds = contracts
            .Select(static contract => contract.ContractId)
            .Where(static contractId => !string.IsNullOrWhiteSpace(contractId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var contractId in activeContractIds)
        {
            lastRun.TryAdd(contractId, DateTimeOffset.MinValue);
        }

        var staleContractIds = lastRun.Keys
            .Where(contractId => !activeContractIds.Contains(contractId))
            .ToList();

        foreach (var staleContractId in staleContractIds)
        {
            lastRun.Remove(staleContractId);
        }
    }
}
