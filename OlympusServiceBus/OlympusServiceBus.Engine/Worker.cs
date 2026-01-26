namespace OlympusServiceBus.Engine;

public sealed class Worker() : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly GraphCalendarService _graphCalendarService;
    private readonly IConfiguration _configuration;

    public Worker(ILogger<Worker> logger, GraphCalendarService graphCalendarService, IConfiguration configuration) : this()
    {
        _logger = logger;
        _graphCalendarService = graphCalendarService;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = _configuration.GetValue("HelloJob:IntervalSeconds", 30);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));

        try
        {
            _logger.LogInformation("Fetching calendar events");
            var meetings = await _graphCalendarService.GetMeetingsAsync(stoppingToken);
            
            _logger.LogInformation("Found {count} meetings", meetings.Count);
            foreach (var meeting in meetings)
            {
                _logger.LogInformation("   {Meeting}", meeting);
            }
        }catch(Exception ex)
        {
            _logger.LogError(ex, "Calendar fetch failed.");
        }

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            _logger.LogInformation("Hello World @ {Time}", DateTimeOffset.Now);
        }
    }
}