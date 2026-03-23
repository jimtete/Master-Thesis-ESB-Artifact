using OlympusServiceBus.RuntimeState.Models;
using OlympusServiceBus.Utils.Contracts.Scheduling;

namespace OlympusServiceBus.Engine.Scheduling;

public static class ContractScheduleDueEvaluator
{
    public static bool IsDue(
        ResolvedContractSchedule schedule,
        ContractExecutionStateEntity? executionState,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return schedule.Mode switch
        {
            ContractScheduleMode.Manual => false,

            ContractScheduleMode.AdHoc => IsAdHocDue(schedule, executionState, nowUtc),

            ContractScheduleMode.Interval => IsIntervalDue(schedule, executionState, nowUtc),

            ContractScheduleMode.Recurring => IsRecurringDue(schedule, executionState, nowUtc),

            _ => false,
        };
    }
    
    private static bool IsAdHocDue(
        ResolvedContractSchedule schedule,
        ContractExecutionStateEntity? executionState,
        DateTimeOffset nowUtc)
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

        return true;
    }
    
    private static bool IsIntervalDue(
        ResolvedContractSchedule schedule,
        ContractExecutionStateEntity? executionState,
        DateTimeOffset? nowUtc)
    {
        if (schedule.Interval is null)
        {
            return false;
        }

        if (executionState?.LastRunStartedAt is null)
        {
            return true;
        }
        
        return executionState?.LastRunStartedAt.Value.Add(schedule.Interval.Value) <= nowUtc;
    }

    private static bool IsRecurringDue(
        ResolvedContractSchedule schedule,
        ContractExecutionStateEntity? executionState,
        DateTimeOffset? nowUtc)
    {
        if (string.IsNullOrWhiteSpace(schedule.CronExpression))
        {
            return false;
        }
        
        var anchor = executionState?.LastRunStartedAt ?? DateTimeOffset.MinValue;

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
}