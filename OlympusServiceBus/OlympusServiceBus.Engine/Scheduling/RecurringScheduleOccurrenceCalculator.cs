using Cronos;

namespace OlympusServiceBus.Engine.Scheduling;

public class RecurringScheduleOccurrenceCalculator
{
    public static DateTimeOffset? GetNextOccurrenceUtc(
        string cronExpression,
        string? timeZoneId,
        DateTimeOffset fromUtc)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new InvalidOperationException("Recurring schedule requires a non-empty CronExpression.");
        }
        
        var expression = CronExpression.Parse(cronExpression);
        var timeZone = ResolveTimeZone(timeZoneId);

        return expression.GetNextOccurrence(fromUtc, timeZone);
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }
        
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }
}