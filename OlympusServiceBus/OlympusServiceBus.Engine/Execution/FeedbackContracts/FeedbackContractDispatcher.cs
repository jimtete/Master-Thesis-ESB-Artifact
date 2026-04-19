namespace OlympusServiceBus.Engine.Execution.FeedbackContracts;

public sealed class FeedbackContractDispatcher
{
    private readonly IFeedbackContractRegistry _feedbackContractRegistry;
    private readonly FeedbackContractExecutionService _feedbackContractExecutionService;
    private readonly ILogger<FeedbackContractDispatcher> _logger;

    public FeedbackContractDispatcher(
        IFeedbackContractRegistry feedbackContractRegistry, 
        FeedbackContractExecutionService feedbackContractExecutionService, 
        ILogger<FeedbackContractDispatcher> logger)
    {
        _feedbackContractRegistry = feedbackContractRegistry;
        _feedbackContractExecutionService = feedbackContractExecutionService;
        _logger = logger;
    }
    
    public async Task DispatchAsync(
        string sourceContractId,
        FeedbackContractExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceContractId))
            throw new ArgumentException("Source contract id is required.", nameof(sourceContractId));

        var feedbackContracts = _feedbackContractRegistry.GetBySourceContractId(sourceContractId);

        if (feedbackContracts.Count == 0)
        {
            _logger.LogDebug(
                "No feedback-contracts registered for source contract {SourceContractId}",
                sourceContractId);

            return;
        }

        _logger.LogDebug(
            "Found {Count} feedback-contract(s) for source contract {SourceContractId}",
            feedbackContracts.Count,
            sourceContractId);

        foreach (var feedbackContract in feedbackContracts)
        {
            if (!FeedbackContractTriggerEvaluator.ShouldExecute(feedbackContract, context.ExecutionStatus))
            {
                _logger.LogDebug(
                    "Skipping feedback-contract {FeedbackContractId} for source contract {SourceContractId} because trigger mode {TriggerMode} does not match execution status {ExecutionStatus}",
                    feedbackContract.ContractId,
                    sourceContractId,
                    feedbackContract.TriggerMode,
                    context.ExecutionStatus);

                continue;
            }

            try
            {
                _logger.LogInformation(
                    "Dispatching feedback-contract {FeedbackContractId} for source contract {SourceContractId} with execution status {ExecutionStatus}",
                    feedbackContract.ContractId,
                    sourceContractId,
                    context.ExecutionStatus);

                await _feedbackContractExecutionService.ExecuteAsync(
                    feedbackContract,
                    context,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to dispatch feedback-contract {FeedbackContractId} for source contract {SourceContractId}",
                    feedbackContract.ContractId,
                    sourceContractId);
            }
        }
    }
}