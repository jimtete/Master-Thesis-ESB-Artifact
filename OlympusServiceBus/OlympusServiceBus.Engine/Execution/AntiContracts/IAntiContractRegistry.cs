using OlympusServiceBus.Utils.Contracts.AntiContracts;

namespace OlympusServiceBus.Engine.Execution.AntiContracts;

public interface IAntiContractRegistry
{
    void SetAllAntiContracts(IEnumerable<AntiContractBase> antiContracts);
    IReadOnlyList<AntiContractBase> GetAllAntiContracts();
    IReadOnlyList<AntiContractBase> GetBySourceContractId(string sourceContractId);
}