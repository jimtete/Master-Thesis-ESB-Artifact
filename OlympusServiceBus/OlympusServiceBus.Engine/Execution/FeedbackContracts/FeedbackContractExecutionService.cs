using OlympusServiceBus.Engine.Helpers;
using OlympusServiceBus.Utils.Contracts.FeedbackContracts;

namespace OlympusServiceBus.Engine.Execution.FeedbackContracts;

public sealed class FeedbackContractExecutionService
{
    private readonly IEnumerable<IFeedbackContractExecutor> _executors;
    private readonly ILogger<FeedbackContractExecutionService> _logger;
    
    public FeedbackContractExecutionService(
        IEnumerable<IFeedbackContractExecutor> executors,
        ILogger<FeedbackContractExecutionService> logger)
    {
        _executors = executors;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        FeedbackContractBase feedbackContract,
        FeedbackContractExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var executor = _executors.FirstOrDefault(x => x.CanExecute(feedbackContract));

        if (executor is null)
        {
            throw new InvalidOperationException(
                $"No feedback-contract executor registered for type {feedbackContract.GetType().Name}");
        }

        _logger.LogDebug(
            "Resolved feedback-contract executor {ExecutorType} for feedback-contract {ContractId}",
            executor.GetType().Name,
            feedbackContract.ContractId);

        await executor.ExecuteAsync(feedbackContract, context, cancellationToken);
    }
}