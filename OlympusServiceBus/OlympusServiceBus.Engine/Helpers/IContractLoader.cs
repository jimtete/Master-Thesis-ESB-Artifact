using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Helpers;

public interface IContractLoader
{
    List<ContractBase> LoadAllContracts(string contractDirectory);
}