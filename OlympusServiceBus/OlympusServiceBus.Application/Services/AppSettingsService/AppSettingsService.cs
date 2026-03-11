using System.IO;
using System.Text.Json;
using OlympusServiceBusApplication.Models;

namespace OlympusServiceBusApplication.Services.AppSettingsService;

public class AppSettingsService : IAppSettingsService
{
    private const string SettingsFileName = "appsettings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    
    public async Task<AppSettings> LoadAsync()
    {
        var filePath = GetSettingsFilePath();

        if (!File.Exists(filePath))
        {
            return new AppSettings();
        }
        
        var json = await File.ReadAllTextAsync(filePath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppSettings();
        }
        
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        return settings ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        
        var filePath = GetSettingsFilePath();
        var directoryPath = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
        
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    private static string GetSettingsFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var applicationDirectory = Path.Combine(appDataPath, "OlympusServiceBus/Configurator");

        return Path.Combine(applicationDirectory, SettingsFileName);
    }
}