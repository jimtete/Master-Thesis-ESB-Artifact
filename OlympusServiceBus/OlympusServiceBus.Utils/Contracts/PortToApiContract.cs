using OlympusServiceBus.Utils.Configuration;

namespace OlympusServiceBus.Utils.Contracts;

public class PortToApiContract : ContractBase
{
    public ListenerConfig Listener { get; set; } = new();
    public ApiConfig Sink { get; set; }
    public ApiFieldConfig[] Mappings { get; set; } = Array.Empty<ApiFieldConfig>();
}

public class ListenerConfig
{
    public string Path { get; set; } = "/";
    public string Method { get; set; } = "POST";
}