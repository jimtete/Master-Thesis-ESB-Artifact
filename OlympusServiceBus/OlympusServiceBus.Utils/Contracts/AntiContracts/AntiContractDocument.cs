using System.Text.Json.Serialization;

namespace OlympusServiceBus.Utils.Contracts.AntiContracts;

public sealed class AntiContractDocument
{
    [JsonPropertyName("ApiStatusAntiContract")]
    public ApiStatusAntiContract? ApiStatusAntiContract { get; set; }
}