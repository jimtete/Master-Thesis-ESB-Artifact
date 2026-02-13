namespace OlympusServiceBus.Engine.Models.Contracts;

public abstract class ContractBase
{
    public string ContractId { get; set; } = Guid.NewGuid().ToString("N");

    public bool Enabled { get; set; }

    public int IntervalSeconds { get; set; }
}