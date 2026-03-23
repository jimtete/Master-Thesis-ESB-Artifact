namespace OlympusServiceBus.Utils.Contracts.Scheduling;

public sealed class ContractScheduleConfiguration
{
    public ContractScheduleMode Mode { get; set; } = ContractScheduleMode.Interval;

    /// <summary>
    /// Used when Mode = AdHoc.
    /// Stored as UTC when possible.
    /// </summary>
    public DateTimeOffset? RunAt { get; set; }

    /// <summary>
    /// Used when Mode = Interval.
    /// </summary>
    public ContractIntervalSchedule? Every { get; set; }

    /// <summary>
    /// Used when Mode = Recurring.
    /// Cron expression or later a higher-level schedule rule.
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Optional timezone identifier for recurring schedules.
    /// Example: Europe/Copenhagen
    /// </summary>
    public string? TimeZone { get; set; }
}