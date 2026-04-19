using OlympusServiceBus.Utils.Contracts.FeedbackContracts;

namespace OlympusServiceBus.Engine.Execution.FeedbackContracts;

public interface IFeedbackContractRegistry
{
    void SetAllFeedbackContracts(IEnumerable<FeedbackContractBase> feedbackContracts);
    IReadOnlyList<FeedbackContractBase> GetAllFeedbackContracts();
    IReadOnlyList<FeedbackContractBase> GetBySourceContractId(string sourceContractId);
}