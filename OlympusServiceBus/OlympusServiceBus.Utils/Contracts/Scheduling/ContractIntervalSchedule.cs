namespace OlympusServiceBus.Utils.Contracts.Scheduling;

public sealed class ContractIntervalSchedule
{
    public int Value { get; set; }
    public IntervalUnit Unit { get; set; } = IntervalUnit.Seconds;
}