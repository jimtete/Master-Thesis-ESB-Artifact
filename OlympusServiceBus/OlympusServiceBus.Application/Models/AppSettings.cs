namespace OlympusServiceBusApplication.Models;

public sealed class AppSettings
{
    public string ContractRootDirectory { get; set; } = string.Empty;
    public string BackgroundRuntimeStartupScriptPath { get; set; } = string.Empty;
}
