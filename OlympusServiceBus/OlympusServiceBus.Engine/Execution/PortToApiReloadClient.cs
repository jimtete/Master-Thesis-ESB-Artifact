namespace OlympusServiceBus.Engine.Execution;

public sealed class PortToApiReloadClient(IHttpClientFactory httpClientFactory, ILogger<PortToApiReloadClient> logger)
{
    public async Task ReloadAsync(string webHostBaseUrl, CancellationToken cancellationToken = default)
    {
        var url = $"{webHostBaseUrl.TrimEnd('/')}/admin/reload";

        try
        {
            var client = httpClientFactory.CreateClient();
            using var resp = await client.PostAsync(url, content: null, cancellationToken);
            resp.EnsureSuccessStatusCode();

            logger.LogInformation("Triggered WebHost reload: {Url}", url);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while triggering reload: {Url}", url);
        }
    }
}