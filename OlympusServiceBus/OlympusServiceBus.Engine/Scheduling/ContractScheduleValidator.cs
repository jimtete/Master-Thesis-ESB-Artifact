using OlympusServiceBus.Utils.Contracts;
using OlympusServiceBus.Utils.Contracts.Scheduling;

namespace OlympusServiceBus.Engine.Scheduling;

public static class ContractScheduleValidator
{
    public static void Validate(ContractBase contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (contract.Schedule is null)
        {
            ValidateLegacyInterval(contract);
            return;
        }

        switch (contract.Schedule.Mode)
        {
            case ContractScheduleMode.Manual:
                ValidateManual(contract);
                break;
            case ContractScheduleMode.AdHoc:
                ValidateAdHoc(contract);
                break;
            case ContractScheduleMode.Interval:
                ValidateInterval(contract);
                break;
            case ContractScheduleMode.Recurring:
                ValidateRecurring(contract);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported schedule mode '{contract.Schedule.Mode}' for contract '{contract.ContractId}'.");
        }
    }
    
    private static void ValidateLegacyInterval(ContractBase contract)
    {
        if (contract.IntervalSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' must define IntervalSeconds > 0 when no Schedule is provided.");
        }
    }
    
    private static void ValidateManual(ContractBase contract)
    {
        if (contract.Schedule is null)
        {
            return;
        }

        ValidateNoScheduledTriggers(contract, "Manual");
    }
    
    private static void ValidateAdHoc(ContractBase contract)
    {
        if (contract.Schedule is null)
            return;

        if (contract.Schedule.RunAt is null)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses AdHoc schedule and must define RunAt.");
        }

        if (contract.Schedule.Every is not null)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses AdHoc schedule and must not define Every.");
        }

        if (!string.IsNullOrWhiteSpace(contract.Schedule.CronExpression))
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses AdHoc schedule and must not define CronExpression.");
        }
    }
    
    private static void ValidateInterval(ContractBase contract)
    {
        if (contract.Schedule?.Every is null)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses Interval schedule and must define Every.");
        }

        if (contract.Schedule.Every.Value <= 0)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses Interval schedule and Every.Value must be greater than zero.");
        }

        if (contract.Schedule.RunAt is not null)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses Interval schedule and must not define RunAt.");
        }

        if (!string.IsNullOrWhiteSpace(contract.Schedule.CronExpression))
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses Interval schedule and must not define CronExpression.");
        }
    }
    
    private static void ValidateRecurring(ContractBase contract)
    {
        if (contract.Schedule is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(contract.Schedule.CronExpression))
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses Recurring schedule and must define CronExpression.");

        }

        if (contract.Schedule.RunAt is not null)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses Recurring schedule and must not define RunAt.");
        }

        if (contract.Schedule.Every is not null)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses Recurring schedule and must not define Every.");
        }
    }

    private static void ValidateNoScheduledTriggers(ContractBase contract, string mode)
    {
        if (contract.Schedule is null)
        {
            return;
        }

        if (contract.Schedule.RunAt is not null)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses {mode} schedule and must not define RunAt.");
        }

        if (contract.Schedule.Every is not null)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses {mode} schedule and must not define Every.");
        }

        if (!string.IsNullOrWhiteSpace(contract.Schedule.CronExpression))
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses {mode} schedule and must not define CronExpression.");
        }
    }
}
