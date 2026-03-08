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
        var contracts = registry.GetContract<FileToApiContract>();

        logger.LogInformation("FileToApiWorker started. Loaded contracts: {Count}", contracts.Count);

        var lastRun = contracts.ToDictionary(c => c.ContractId, _ => DateTimeOffset.MinValue);

        while (!stoppingToken.IsCancellationRequested)
        {
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
}