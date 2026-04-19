using OlympusServiceBus.Utils.Contracts.FeedbackContracts;

namespace OlympusServiceBus.Engine.Execution.FeedbackContracts;

public static class FeedbackContractTriggerEvaluator
{
    public static bool ShouldExecute(
        FeedbackContractBase feedbackContract,
        string executionStatus)
    {
        if (feedbackContract is null)
            throw new ArgumentNullException(nameof(feedbackContract));

        if (string.IsNullOrWhiteSpace(feedbackContract.TriggerMode))
            return false;

        var triggerMode = feedbackContract.TriggerMode.Trim();

        if (string.Equals(triggerMode, "Always", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(triggerMode, "OnSuccess", StringComparison.OrdinalIgnoreCase))
            return string.Equals(executionStatus, "Success", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(triggerMode, "OnFailure", StringComparison.OrdinalIgnoreCase))
            return string.Equals(executionStatus, "Failed", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(triggerMode, "OnPartialSuccess", StringComparison.OrdinalIgnoreCase))
            return string.Equals(executionStatus, "PartiallySucceeded", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(triggerMode, "OnSkipped", StringComparison.OrdinalIgnoreCase))
            return string.Equals(executionStatus, "Skipped", StringComparison.OrdinalIgnoreCase);

        return false;
    }
}