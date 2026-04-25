using Microsoft.Extensions.Options;

namespace OlympusServiceBus.Engine.Execution;

public sealed class PortToApiReloadClient(
    IHttpClientFactory httpClientFactory,
    IOptions<WebHostOptions> webHostOptions,
    ILogger<PortToApiReloadClient> logger)
{
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var webHostBaseUrl = string.IsNullOrWhiteSpace(webHostOptions.Value.BaseUrl)
            ? "http://localhost:5099"
            : webHostOptions.Value.BaseUrl;

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
