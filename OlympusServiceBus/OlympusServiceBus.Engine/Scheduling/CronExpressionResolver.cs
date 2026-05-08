using Cronos;

namespace OlympusServiceBus.Engine.Scheduling;

internal static class CronExpressionResolver
{
    public static CronExpression Parse(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            throw new InvalidOperationException("Recurring schedule requires a non-empty CronExpression.");
        }

        var trimmedCronExpression = cronExpression.Trim();
        var fieldCount = trimmedCronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        return fieldCount switch
        {
            5 => CronExpression.Parse(trimmedCronExpression, CronFormat.Standard),
            6 => CronExpression.Parse(trimmedCronExpression, CronFormat.IncludeSeconds),
            _ => throw new CronFormatException(
                $"Invalid CRON expression '{trimmedCronExpression}'. Expected 5 fields or 6 fields including seconds.")
        };
    }
}
