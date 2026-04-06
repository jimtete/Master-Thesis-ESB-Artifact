namespace OlympusServiceBusApplication.Models.Contracts;

public class ScheduleEditorRequest
{
    // Manual, AdHoc, Interval, Recurring
    public string Mode { get; set; } = "Manual";

    // Used for AdHoc
    public DateTimeOffset? RunAt { get; set; }

    // Used for Interval
    public int IntervalValue { get; set; } = 1;
    public string IntervalUnit { get; set; } = "Minutes";

    // Used for CRON in the UI, maps to backend Recurring
    public string CronExpression { get; set; } = string.Empty;

    // Used for AdHoc / Recurring
    public string TimeZone { get; set; } = "UTC";
}