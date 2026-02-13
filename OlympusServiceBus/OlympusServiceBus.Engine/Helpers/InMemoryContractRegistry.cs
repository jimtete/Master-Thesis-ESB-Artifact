using OlympusServiceBus.Engine.Models.Contracts;

namespace OlympusServiceBus.Engine.Helpers;

public class InMemoryContractRegistry : IContractRegistry
{
    private readonly List<ContractBase> _contracts = new();

    public IReadOnlyList<ContractBase> AllContracts => _contracts;

    public IReadOnlyList<T> GetContract<T>() where T : ContractBase
        => _contracts.OfType<T>().ToList();

    public void SetAllContracts(IEnumerable<ContractBase> contracts)
    {
        _contracts.Clear();
        _contracts.AddRange(contracts);
    }
}