using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Configuration.File;

namespace OlympusServiceBus.Utils.Contracts;

public class PortToFileContract : ContractBase
{
    public ListenerConfig Listener { get; set; } = new();
    public FileSinkConfig Sink { get; set; } = new();
    public PortToApiRequest? Request { get; set; }
    public ApiFieldConfig[] Mappings { get; set; } = Array.Empty<ApiFieldConfig>();
}