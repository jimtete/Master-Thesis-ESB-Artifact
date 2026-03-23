using OlympusServiceBus.Utils.Contracts;
using OlympusServiceBus.Utils.Contracts.Scheduling;

namespace OlympusServiceBus.Engine.Scheduling;

public static class ContractScheduleResolver
{
    public static ResolvedContractSchedule Resolve(ContractBase contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (contract.Schedule is null)
        {
            return ResolveLegacySchedule(contract);
        }

        return contract.Schedule.Mode switch
        {
            ContractScheduleMode.Manual => new ResolvedContractSchedule
            {
                Mode = ContractScheduleMode.Manual
            },

            ContractScheduleMode.AdHoc => new ResolvedContractSchedule
            {
                Mode = ContractScheduleMode.AdHoc,
                RunAtUtc = contract.Schedule.RunAt?.ToUniversalTime()
            },

            ContractScheduleMode.Interval => new ResolvedContractSchedule
            {
                Mode = ContractScheduleMode.Interval,
                Interval = ResolveInterval(contract.Schedule.Every)
            },

            ContractScheduleMode.Recurring => new ResolvedContractSchedule
            {
                Mode = ContractScheduleMode.Recurring,
                CronExpression = ResolveCronExpression(contract),
                TimeZone = ResolveTimeZone(contract.Schedule.TimeZone)
            },

            _ => throw new InvalidOperationException(
                $"Unsupported schedule mode '{contract.Schedule.Mode}' for contract '{contract.ContractId}'.")
        };
    }

    private static ResolvedContractSchedule ResolveLegacySchedule(ContractBase contract)
    {
        if (contract.IntervalSeconds <= 0)
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' must define IntervalSeconds > 0 when no Schedule is provided.");
        }

        return new ResolvedContractSchedule
        {
            Mode = ContractScheduleMode.Interval,
            Interval = TimeSpan.FromSeconds(contract.IntervalSeconds)
        };
    }

    private static TimeSpan ResolveInterval(ContractIntervalSchedule? interval)
    {
        if (interval is null)
        {
            throw new InvalidOperationException("Schedule mode Interval requires a non-null Every configuration.");
        }

        if (interval.Value <= 0)
        {
            throw new InvalidOperationException("Interval schedule value must be greater than zero.");
        }

        return interval.Unit switch
        {
            IntervalUnit.Seconds => TimeSpan.FromSeconds(interval.Value),
            IntervalUnit.Minutes => TimeSpan.FromMinutes(interval.Value),
            IntervalUnit.Hours => TimeSpan.FromHours(interval.Value),
            IntervalUnit.Days => TimeSpan.FromDays(interval.Value),
            _ => throw new InvalidOperationException($"Unsupported interval unit '{interval.Unit}'.")
        };
    }

    private static string ResolveCronExpression(ContractBase contract)
    {
        var cronExpression = contract.Schedule?.CronExpression?.Trim();

        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new InvalidOperationException(
                $"Contract '{contract.ContractId}' uses Recurring schedule and must define a non-empty CronExpression.");
        }

        return cronExpression;
    }

    private static string? ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return timeZoneId;
    }
}