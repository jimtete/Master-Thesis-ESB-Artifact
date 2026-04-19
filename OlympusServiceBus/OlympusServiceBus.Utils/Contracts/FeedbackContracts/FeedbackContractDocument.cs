using System.Text.Json.Serialization;

namespace OlympusServiceBus.Utils.Contracts.FeedbackContracts;

public sealed class FeedbackContractDocument
{
    [JsonPropertyName("ApiStatusFeedbackContract")]
    public ApiStatusFeedbackContract? ApiStatusFeedbackContract { get; set; }
}