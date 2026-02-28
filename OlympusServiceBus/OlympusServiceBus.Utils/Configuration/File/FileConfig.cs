namespace OlympusServiceBus.Utils.Configuration.File;

public class FileConfig
{
    public string Directory { get; set; } = "";
    public string SearchPattern { get; set; } = ".csv";
    public bool IncludeSubdirectories { get; set; } = false;
    public string? ProcessedDirectory { get; set; }
    public string? ErrorDirectory { get; set; }
}