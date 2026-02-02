namespace OlympusServiceBus.Engine.Models.Configuration;

public class ApiRequestConfig
{
    public string Endpoint { get; set; } = "";
    public string Method { get; set; } = "GET";

    public int TimeoutSeconds { get; set; } = 10;

    public Dictionary<string, string> Headers { get; set; }
    public Dictionary<string, string> Query { get; set; }

    public BodyConfig Body { get; set; }
}

public class BodyConfig
{
    public string Mode { get; set; } = "None";

    public string? EnvelopePropertyName { get; set; }
}