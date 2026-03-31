namespace OlympusServiceBus.Utils.Configuration.File;

public sealed class FileSinkConfig
{
    public string Directory { get; set; }
    public string FileExtension { get; set; } = "csv";
}