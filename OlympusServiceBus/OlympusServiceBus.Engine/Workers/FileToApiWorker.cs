using OlympusServiceBus.Engine.Execution.FileToApi;
using OlympusServiceBus.Engine.Helpers;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Workers;

public sealed class FileToApiWorker
(
    ILogger<FileToApiWorker> logger,
    IContractRegistry registry,
    IServiceScopeFactory scopeFactory
) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("FileToApiWorker started.");

        var lastRun = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

        while (!stoppingToken.IsCancellationRequested)
        {
            var contracts = registry.GetContract<FileToApiContract>();
            SyncLastRun(lastRun, contracts);

            foreach (var contract in contracts)
            {
                if (!contract.Enabled)
                    continue;

                var dueAfter = TimeSpan.FromSeconds(Math.Max(1, contract.IntervalSeconds));
                var nextDue = lastRun[contract.ContractId] + dueAfter;

                if (DateTimeOffset.UtcNow >= nextDue)
                {
                    using var scope = scopeFactory.CreateScope();
                    var executor = scope.ServiceProvider.GetRequiredService<FileToApiExecutor>();

                    await executor.ExecuteOnce(contract, stoppingToken);
                    lastRun[contract.ContractId] = DateTimeOffset.UtcNow;
                }
            }

            await Task.Delay(Tick, stoppingToken);
        }
    }

    private static void SyncLastRun(
        IDictionary<string, DateTimeOffset> lastRun,
        IReadOnlyList<FileToApiContract> contracts)
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
