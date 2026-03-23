namespace OlympusServiceBus.Engine.Execution;

public sealed class WebHostReloadOnStartup : IHostedService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebHostReloadOnStartup> _logger;

    public WebHostReloadOnStartup(
        IHttpClientFactory httpClientFactory,
        ILogger<WebHostReloadOnStartup> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const string reloadUrl = "http://localhost:5099/admin/reload";

        try
        {
            var client = _httpClientFactory.CreateClient();

            using var response = await client.PostAsync(reloadUrl, content: null, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "WebHost reload endpoint responded with status code {StatusCode}: {Url}",
                    (int)response.StatusCode,
                    reloadUrl);

                return;
            }

            _logger.LogInformation("WebHost reload triggered successfully: {Url}", reloadUrl);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                ex,
                "WebHost reload endpoint is unavailable. Continuing startup without reload: {Url}",
                reloadUrl);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "WebHost reload timed out. Continuing startup without reload: {Url}",
                reloadUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unexpected error while triggering WebHost reload. Continuing startup: {Url}",
                reloadUrl);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}