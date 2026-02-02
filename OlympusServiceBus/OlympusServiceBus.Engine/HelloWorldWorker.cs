namespace OlympusServiceBus.Engine;

public class HelloWorldWorker(ILogger<HelloWorldWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("HelloWorldWorker started at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Hello World at: {time}", DateTimeOffset.Now);

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}