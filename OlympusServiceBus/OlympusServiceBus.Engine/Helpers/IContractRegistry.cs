using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Helpers;

public interface IContractRegistry
{
    IReadOnlyList<ContractBase> AllContracts { get; }
    IReadOnlyList<T> GetContract<T>() where T : ContractBase;
    void SetAllContracts(IEnumerable<ContractBase> contracts);
}