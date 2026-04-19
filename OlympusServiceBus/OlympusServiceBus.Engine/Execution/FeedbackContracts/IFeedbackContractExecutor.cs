using OlympusServiceBus.Utils.Contracts.FeedbackContracts;

namespace OlympusServiceBus.Engine.Execution.FeedbackContracts;

public interface IFeedbackContractExecutor
{
    bool CanExecute(FeedbackContractBase feedbackContract);

    Task ExecuteAsync(
        FeedbackContractBase feedbackContract,
        FeedbackContractExecutionContext context,
        CancellationToken cancellationToken = default
    );
}