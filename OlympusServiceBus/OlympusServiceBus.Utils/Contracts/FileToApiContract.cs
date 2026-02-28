using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Configuration.File;

namespace OlympusServiceBus.Utils.Contracts;

public sealed class FileToApiContract : ContractBase
{
    public FileConfig Source { get; set; } = new();
    public ApiConfig Sink { get; set; } = new();

    public ApiFieldConfig[] Mappings { get; set; }
    public FileRules Rules { get; set; }
    public PortToApiRequest? Request { get; set; }
}