using OlympusServiceBus.Engine.Models.Contracts;

namespace OlympusServiceBus.Engine.Helpers;

public interface IContractLoader
{
    List<ContractBase> LoadAllContracts(string contractDirectory);
}