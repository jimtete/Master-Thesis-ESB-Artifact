using OlympusServiceBus.RuntimeState.Models;
using OlympusServiceBus.Utils.Contracts.Scheduling;

namespace OlympusServiceBus.Engine.Scheduling;

public static class ContractScheduleDueEvaluator
{
    public static bool IsDue(
        ResolvedContractSchedule schedule,
        ContractExecutionStateEntity? executionState,
        DateTimeOffset nowUtc,
        DateTimeOffset? activationStartedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return schedule.Mode switch
        {
            ContractScheduleMode.Manual => false,
            ContractScheduleMode.AdHoc => IsAdHocDue(schedule, executionState, nowUtc, activationStartedAtUtc),
            ContractScheduleMode.Interval => IsIntervalDue(schedule, executionState, nowUtc, activationStartedAtUtc),
            ContractScheduleMode.Recurring => IsRecurringDue(schedule, executionState, nowUtc, activationStartedAtUtc),
            _ => false,
        };
    }

    private static bool IsAdHocDue(
        ResolvedContractSchedule schedule,
        ContractExecutionStateEntity? executionState,
        DateTimeOffset nowUtc,
        DateTimeOffset? activationStartedAtUtc)
    {
        if (schedule.RunAtUtc is null)
        {
            return false;
        }

        if (schedule.RunAtUtc > nowUtc)
        {
            return false;
        }

        if (executionState?.LastRunCompletedAt is not null &&
            executionState.LastRunCompletedAt >= schedule.RunAtUtc)
        {
            return false;
        }

        if (activationStartedAtUtc is not null &&
            activationStartedAtUtc > schedule.RunAtUtc)
        {
            return false;
        }

        return true;
    }

    private static bool IsIntervalDue(
        ResolvedContractSchedule schedule,
        ContractExecutionStateEntity? executionState,
        DateTimeOffset nowUtc,
        DateTimeOffset? activationStartedAtUtc)
    {
        if (schedule.Interval is null)
        {
            return false;
        }

        var anchor = Max(executionState?.LastRunStartedAt, activationStartedAtUtc);

        if (anchor is null)
        {
            return true;
        }

        var nextDue = anchor.Value.Add(schedule.Interval.Value);
        return nextDue <= nowUtc;
    }

    private static bool IsRecurringDue(
        ResolvedContractSchedule schedule,
        ContractExecutionStateEntity? executionState,
        DateTimeOffset nowUtc,
        DateTimeOffset? activationStartedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(schedule.CronExpression))
        {
            return false;
        }

        var anchor = Max(executionState?.LastRunStartedAt, activationStartedAtUtc) ?? DateTimeOffset.MinValue;

        var nextOccurrence = RecurringScheduleOccurrenceCalculator.GetNextOccurrenceUtc(
            schedule.CronExpression,
            schedule.TimeZone,
            anchor);

        if (nextOccurrence is null)
        {
            return false;
        }

        return nextOccurrence <= nowUtc;
    }

    private static DateTimeOffset? Max(DateTimeOffset? left, DateTimeOffset? right)
    {
        return (left, right) switch
        {
            (null, null) => null,
            ({ } value, null) => value,
            (null, { } value) => value,
            ({ } leftValue, { } rightValue) => leftValue >= rightValue ? leftValue : rightValue
        };
    }
}
