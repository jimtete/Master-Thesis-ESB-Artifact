using OlympusServiceBus.Engine.Execution;
using OlympusServiceBus.Engine.Helpers;
using OlympusServiceBus.Engine.Services;
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
        var contracts = registry.GetContract<ApiToApiContract>();

        logger.LogInformation("ApiToApiWorker started. Loaded contracts: {Count}", contracts.Count);

        // last-run tracking per contract
        var lastRun = contracts.ToDictionary(c => c.ContractId, _ => DateTimeOffset.MinValue);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var c in contracts)
            {
                if (!c.Enabled) continue;

                var dueAfter = TimeSpan.FromSeconds(Math.Max(1, c.IntervalSeconds));
                var nextDue = lastRun[c.ContractId] + dueAfter;

                if (DateTimeOffset.UtcNow >= nextDue)
                {
                    using var scope = scopeFactory.CreateScope();
                    var executionService = scope.ServiceProvider.GetRequiredService<IApiToApiExecutionService>();
                    await executionService.ExecuteAsync(c, stoppingToken);
                    lastRun[c.ContractId] = DateTimeOffset.UtcNow;
                }
            }

            await Task.Delay(Tick, stoppingToken);
        }
    }
}