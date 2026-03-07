namespace OlympusServiceBus.RuntimeState.Models;

public class ContractExecutionStateEntity
{
    public string ContractId { get; set; } = null;
    public string ContractName { get; set; } = null;

    public DateTimeOffset? LastRunStartedAt { get; set; }
    public DateTimeOffset? LastRunCompletedAt { get; set; }
    public string? LastRunStatus { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}