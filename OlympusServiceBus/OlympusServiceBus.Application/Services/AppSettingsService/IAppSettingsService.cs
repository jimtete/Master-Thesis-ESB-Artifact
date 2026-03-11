using OlympusServiceBusApplication.Models;

namespace OlympusServiceBusApplication.Services.AppSettingsService;

public interface IAppSettingsService
{
    Task<AppSettings> LoadAsync();
    Task SaveAsync(AppSettings settings);
}