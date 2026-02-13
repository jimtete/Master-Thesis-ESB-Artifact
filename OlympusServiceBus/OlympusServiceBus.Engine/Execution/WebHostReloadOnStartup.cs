namespace OlympusServiceBus.Engine.Execution;

public sealed class WebHostReloadOnStartup(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<WebHostReloadOnStartup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var baseUrl = configuration["WebHost:BaseUrl"];

        if (string.IsNullOrEmpty(baseUrl))
        {
            logger.LogWarning("WebHost:BaseUrl is missing in web.config");
            return;
        }
        
        var url = $"{baseUrl.TrimEnd('/')}/admin/reload";

        try
        {
            var client = httpClientFactory.CreateClient();
            using var resp = await client.PostAsync(url, content: null, cancellationToken);
            resp.EnsureSuccessStatusCode();

            logger.LogInformation("Triggered WebHost reload on startup: {Url}", url);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger WebHost reload on startup: {Url}", url);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}