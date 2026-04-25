using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Helpers;

public class InMemoryContractRegistry : IContractRegistry
{
    private readonly object _sync = new();
    private List<ContractBase> _contracts = new();

    public IReadOnlyList<ContractBase> AllContracts
    {
        get
        {
            lock (_sync)
            {
                return _contracts.ToList();
            }
        }
    }

    public IReadOnlyList<T> GetContract<T>() where T : ContractBase
    {
        lock (_sync)
        {
            return _contracts.OfType<T>().ToList();
        }
    }

    public void SetAllContracts(IEnumerable<ContractBase> contracts)
    {
        ArgumentNullException.ThrowIfNull(contracts);

        lock (_sync)
        {
            _contracts = contracts.ToList();
        }
    }
}
