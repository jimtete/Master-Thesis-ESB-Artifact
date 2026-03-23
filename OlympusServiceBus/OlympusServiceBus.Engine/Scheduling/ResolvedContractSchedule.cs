using OlympusServiceBus.Utils.Contracts.Scheduling;

namespace OlympusServiceBus.Engine.Scheduling;

public class ResolvedContractSchedule
{
    public ContractScheduleMode Mode { get; set; }
    public DateTimeOffset? RunAtUtc { get; set; }
    public TimeSpan? Interval { get; set; }
    public string? CronExpression { get; set; }
    public string? TimeZone { get; set; }
}