using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Scheduling;

public static class ContractSchedulingBootstrapValidator
{
    public static void ValidateAll(IEnumerable<ContractBase> contracts)
    {
        foreach (var contract in contracts)
        {
            if (!RequiresEngineScheduling(contract))
            {
                continue;
            }

            ContractScheduleValidator.Validate(contract);
        }
    }

    private static bool RequiresEngineScheduling(ContractBase contract)
    {
        return contract is ApiToApiContract
            or ApiToFileContract
            or FileToApiContract
            or FileToFileContract;
    }
}