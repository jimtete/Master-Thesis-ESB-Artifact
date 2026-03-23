using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Scheduling;

public static class ContractSchedulingBootstrapValidator
{
    public static void ValidateAll(IEnumerable<ContractBase> contracts)
    {
        ArgumentNullException.ThrowIfNull(contracts);

        foreach (var contract in contracts)
        {
            ContractScheduleValidator.Validate(contract);
        }
    }
}