using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Configuration.File;

namespace OlympusServiceBus.Utils.Contracts;

public sealed class ApiToFileContract : ContractBase
{
    public ApiConfig Source { get; set; } = new();
    public FileSinkConfig Sink { get; set; } = new();
    public ApiFieldConfig[] Mappings { get; set; } = Array.Empty<ApiFieldConfig>();
}