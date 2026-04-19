namespace OlympusServiceBus.Utils.Contracts.FeedbackContracts;

public sealed class ApiStatusFeedbackContract : FeedbackContractBase
{
    /// <summary>
    /// Logical target system that will receive the reverse-direction status message.
    /// </summary>
    public string TargetSystem { get; set; } = string.Empty;

    /// <summary>
    /// Relative or absolute endpoint used to publish the status response.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// HTTP method for the FeedbackContract call.
    /// Suggested values: POST, PUT, PATCH
    /// </summary>
    public string Method { get; set; } = "POST";

    /// <summary>
    /// Maps runtime values into the outbound FeedbackContract payload.
    /// Key = outbound field name
    /// Value = source expression / field path
    /// </summary>
    public Dictionary<string, string> PayloadMappings { get; set; } = new();

    /// <summary>
    /// Optional static values always included in the outgoing payload.
    /// </summary>
    public Dictionary<string, string> StaticPayload { get; set; } = new();

    /// <summary>
    /// Maps local execution outcomes into normalized business statuses.
    /// Example: Success -> Delivered, Failed -> Rejected
    /// </summary>
    public Dictionary<string, string> StatusMappings { get; set; } = new();

    /// <summary>
    /// Optional timeout in seconds for the outbound FeedbackContract call.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Whether the FeedbackContract should be attempted again on transient failure.
    /// </summary>
    public bool RetryOnFailure { get; set; } = true;
}