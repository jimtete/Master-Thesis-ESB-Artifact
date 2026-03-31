using OlympusServiceBus.Utils.Configuration;
using OlympusServiceBus.Utils.Configuration.File;

namespace OlympusServiceBus.Utils.Contracts;

public sealed class FileToFileContract
{
    public FileConfig Source { get; set; } = new();
    public FileSinkConfig Sink { get; set; } = new();
    public ApiFieldConfig[] Mappings { get; set; } = Array.Empty<ApiFieldConfig>();
    public FileRules Rules { get; set; } = new();
    public PortToApiRequest? Request { get; set; }
}