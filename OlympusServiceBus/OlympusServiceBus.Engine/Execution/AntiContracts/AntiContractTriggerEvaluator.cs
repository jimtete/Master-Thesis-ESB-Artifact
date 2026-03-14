using OlympusServiceBus.Utils.Contracts.AntiContracts;

namespace OlympusServiceBus.Engine.Execution.AntiContracts;

public static class AntiContractTriggerEvaluator
{
    public static bool ShouldExecute(
        AntiContractBase antiContract,
        string executionStatus)
    {
        if (antiContract is null)
            throw new ArgumentNullException(nameof(antiContract));

        if (string.IsNullOrWhiteSpace(antiContract.TriggerMode))
            return false;

        var triggerMode = antiContract.TriggerMode.Trim();

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