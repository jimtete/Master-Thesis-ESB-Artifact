using Microsoft.Graph;

namespace OlympusServiceBus.Engine;

public sealed class GraphCalendarService
{
    private readonly GraphServiceClient _graph;
    private readonly GraphOptions _graphOptions;

    public GraphCalendarService(GraphServiceClient graph, GraphOptions options)
    {
        _graph = graph;
        _graphOptions = options;
    }

    public async Task<IReadOnlyList<string>> GetMeetingsAsync(CancellationToken cancellationToken)
    {
        var start = DateTimeOffset.UtcNow.AddDays(-7).ToString("o");
        var end = DateTimeOffset.UtcNow.AddDays(7).ToString("o");

        var result = await _graph.Me.CalendarView.GetAsync(
            cfg =>
        {
            cfg.QueryParameters.StartDateTime = start;
            cfg.QueryParameters.EndDateTime = end;
            cfg.QueryParameters.Top = 50;
        });
        
        var lines = new List<string>();
        foreach (var ev in result?.Value ?? [])
        {
            var subject = ev.Subject ?? "(no subject)";
            var s = ev.Start?.DateTime ?? "?";
            var e = ev.End?.DateTime ?? "?";
            lines.Add($"{s} -> {e} | {subject}");
        }
        
        return lines;
    }
}