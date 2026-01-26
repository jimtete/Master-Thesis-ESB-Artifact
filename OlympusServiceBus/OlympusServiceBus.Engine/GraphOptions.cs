namespace OlympusServiceBus.Engine;

public class GraphOptions
{
    public string TenantId { get; set; } = "common";
    public string ClientId { get; set; } = "";
    public string[] Scopes { get; set; } = [];
    public string Timezone { get; set; } = "Europe/Copenhagen";
}