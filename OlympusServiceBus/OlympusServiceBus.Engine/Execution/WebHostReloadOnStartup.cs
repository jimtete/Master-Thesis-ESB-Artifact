namespace OlympusServiceBus.Engine.Execution;

public sealed class WebHostReloadOnStartup(PortToApiReloadClient reloadClient) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => reloadClient.ReloadAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
