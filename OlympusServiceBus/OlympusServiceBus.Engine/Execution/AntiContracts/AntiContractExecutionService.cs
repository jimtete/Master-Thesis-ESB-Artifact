using OlympusServiceBus.Engine.Helpers;
using OlympusServiceBus.Utils.Contracts.AntiContracts;

namespace OlympusServiceBus.Engine.Execution.AntiContracts;

public sealed class AntiContractExecutionService
{
    private readonly IEnumerable<IAntiContractExecutor> _executors;
    private readonly ILogger<AntiContractExecutionService> _logger;
    
    public AntiContractExecutionService(
        IEnumerable<IAntiContractExecutor> executors,
        ILogger<AntiContractExecutionService> logger)
    {
        _executors = executors;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        AntiContractBase antiContract,
        AntiContractExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var executor = _executors.FirstOrDefault(x => x.CanExecute(antiContract));

        if (executor is null)
        {
            throw new InvalidOperationException(
                $"No anti-contract executor registered for type {antiContract.GetType().Name}");
        }

        _logger.LogDebug(
            "Resolved anti-contract executor {ExecutorType} for anti-contract {ContractId}",
            executor.GetType().Name,
            antiContract.ContractId);

        await executor.ExecuteAsync(antiContract, context, cancellationToken);
    }
}