using OlympusServiceBusApplication.Models;

namespace OlympusServiceBusApplication.Services.BackgroundRuntimeService;

public interface IBackgroundRuntimeService
{
    string SwaggerUiUrl { get; }

    Task<BackgroundRuntimeResult> EnsureStartedAsync();

    Task<BackgroundRuntimeResult> OpenSwaggerUiAsync();
}
