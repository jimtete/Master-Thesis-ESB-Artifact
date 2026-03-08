namespace OlympusServiceBus.RuntimeState.Models;

public class ContractMessageStateEntity
{
    public long Id { get; set; }

    public string ContractId { get; set; } = null;
    public string ContractName { get; set; } = null;

    public string BusinessKey { get; set; } = null;
    public string PayloadHash { get; set; } = null;

    public string? CanonicalSnapshot { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? LastPublishedAt { get; set; }
}