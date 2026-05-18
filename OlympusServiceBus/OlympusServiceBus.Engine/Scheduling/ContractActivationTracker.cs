namespace OlympusServiceBus.Engine.Scheduling;

public sealed class ContractActivationTracker
{
    private readonly Dictionary<string, bool> _knownEnabledStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _activationStartedAt = new(StringComparer.OrdinalIgnoreCase);

    public DateTimeOffset? Observe(string contractId, bool isEnabled, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(contractId))
        {
            return null;
        }

        if (!_knownEnabledStates.TryGetValue(contractId, out var wasEnabled))
        {
            _knownEnabledStates[contractId] = isEnabled;
            return null;
        }

        if (!wasEnabled && isEnabled)
        {
            _activationStartedAt[contractId] = nowUtc;
        }
        else if (wasEnabled && !isEnabled)
        {
            _activationStartedAt.Remove(contractId);
        }

        _knownEnabledStates[contractId] = isEnabled;

        return _activationStartedAt.TryGetValue(contractId, out var activationStartedAt)
            ? activationStartedAt
            : null;
    }

    public void MarkExecuted(string contractId)
    {
        if (string.IsNullOrWhiteSpace(contractId))
        {
            return;
        }

        _activationStartedAt.Remove(contractId);
    }

    public void SyncKnownContracts(IEnumerable<string> activeContractIds)
    {
        ArgumentNullException.ThrowIfNull(activeContractIds);

        var activeIds = activeContractIds
            .Where(static contractId => !string.IsNullOrWhiteSpace(contractId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var staleId in _knownEnabledStates.Keys.Where(contractId => !activeIds.Contains(contractId)).ToList())
        {
            _knownEnabledStates.Remove(staleId);
            _activationStartedAt.Remove(staleId);
        }
    }
}
