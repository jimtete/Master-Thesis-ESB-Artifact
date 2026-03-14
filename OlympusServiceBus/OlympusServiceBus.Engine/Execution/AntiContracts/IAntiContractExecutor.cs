using OlympusServiceBus.Utils.Contracts.AntiContracts;

namespace OlympusServiceBus.Engine.Execution.AntiContracts;

public interface IAntiContractExecutor
{
    bool CanExecute(AntiContractBase antiContract);

    Task ExecuteAsync(
        AntiContractBase antiContract,
        AntiContractExecutionContext context,
        CancellationToken cancellationToken = default
    );
}