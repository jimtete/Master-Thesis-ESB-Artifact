using OlympusServiceBus.Utils.Contracts;
using OlympusServiceBus.Utils.Contracts.AntiContracts;

namespace OlympusServiceBus.Engine.Helpers;

public interface IContractLoader
{
    List<ContractBase> LoadAllContracts(string contractDirectory);
    List<AntiContractBase> LoadAllAntiContracts(string contractDirectory);
}