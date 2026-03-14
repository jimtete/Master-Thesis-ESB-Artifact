using OlympusServiceBus.Utils.Contracts.AntiContracts;

namespace OlympusServiceBus.Engine.Execution.AntiContracts;

public sealed class InMemoryAntiContractRegistry : IAntiContractRegistry
{
    private readonly object _sync = new();
    private List<AntiContractBase> _antiContracts = new();
    
    public void SetAllAntiContracts(IEnumerable<AntiContractBase> antiContracts)
    {
        if (antiContracts is null)
        {
            throw new ArgumentNullException(nameof(antiContracts));
        }

        lock (_sync)
        {
            _antiContracts = antiContracts.ToList();
        }
    }

    public IReadOnlyList<AntiContractBase> GetAllAntiContracts()
    {
        lock (_sync)
        {
            return _antiContracts.ToList();
        }
    }

    public IReadOnlyList<AntiContractBase> GetBySourceContractId(string sourceContractId)
    {
        if (string.IsNullOrWhiteSpace(sourceContractId))
        {
            return Array.Empty<AntiContractBase>();
        }

        lock (_sync)
        {
            return _antiContracts
                .Where(x => string.Equals(
                    x.SourceContractId,
                    sourceContractId,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}