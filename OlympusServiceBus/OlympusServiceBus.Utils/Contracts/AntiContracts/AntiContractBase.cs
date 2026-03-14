namespace OlympusServiceBus.Utils.Contracts.AntiContracts;

public abstract class AntiContractBase
{
    public string ContractId { get; set; } = string.Empty;
    public string ContractType { get; set; } = string.Empty;

    /// <summary>
    /// Logical name of the originating flow or source contract.
    /// Helps correlate the Anti-Contract with the forward Contract.
    /// </summary>
    public string SourceContractId { get; set; } = string.Empty;

    /// <summary>
    /// Defines whether this Anti-Contract is triggered after success,
    /// failure, or always.
    /// Suggested values: OnSuccess, OnFailure, Always
    /// </summary>
    public string TriggerMode { get; set; } = "OnFailure";

    /// <summary>
    /// Correlation fields used to connect outbound status information
    /// back to the original business operation.
    /// </summary>
    public List<string> CorrelationFields { get; set; } = new();

    /// <summary>
    /// Optional metadata for observability, classification, or tagging.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}