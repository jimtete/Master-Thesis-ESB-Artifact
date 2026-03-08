using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace OlympusServiceBus.Engine.Execution.PortToApi;

public class PortToApiPayloadHashProvider
{
    public string ComputeHash(JsonObject payload)
    {
        var json = payload.ToJsonString();

        var bytes = Encoding.UTF8.GetBytes(json);
        var hashBytes = SHA256.HashData(bytes);
        
        return Convert.ToHexString(hashBytes);
    }
}