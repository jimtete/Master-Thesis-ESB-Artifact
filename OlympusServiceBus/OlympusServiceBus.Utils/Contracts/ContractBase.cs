namespace OlympusServiceBus.Utils.Contracts;

public abstract class ContractBase
{
    public string ContractId { get; set; } = Guid.NewGuid().ToString("N");

    public required string Name { get; set; }
    
    public bool Enabled { get; set; }

    public int IntervalSeconds { get; set; }
}