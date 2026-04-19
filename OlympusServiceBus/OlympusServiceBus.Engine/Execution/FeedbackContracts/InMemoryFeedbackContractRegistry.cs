using OlympusServiceBus.Utils.Contracts.FeedbackContracts;

namespace OlympusServiceBus.Engine.Execution.FeedbackContracts;

public sealed class InMemoryFeedbackContractRegistry : IFeedbackContractRegistry
{
    private readonly object _sync = new();
    private List<FeedbackContractBase> _feedbackContracts = new();
    
    public void SetAllFeedbackContracts(IEnumerable<FeedbackContractBase> feedbackContracts)
    {
        if (feedbackContracts is null)
        {
            throw new ArgumentNullException(nameof(feedbackContracts));
        }

        lock (_sync)
        {
            _feedbackContracts = feedbackContracts.ToList();
        }
    }

    public IReadOnlyList<FeedbackContractBase> GetAllFeedbackContracts()
    {
        lock (_sync)
        {
            return _feedbackContracts.ToList();
        }
    }

    public IReadOnlyList<FeedbackContractBase> GetBySourceContractId(string sourceContractId)
    {
        if (string.IsNullOrWhiteSpace(sourceContractId))
        {
            return Array.Empty<FeedbackContractBase>();
        }

        lock (_sync)
        {
            return _feedbackContracts
                .Where(x => string.Equals(
                    x.SourceContractId,
                    sourceContractId,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}