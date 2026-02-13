namespace OlympusServiceBus.Utils.Configuration;

public class ApiToApi
{
    public int IntervalSeconds { get; set; }

    public ApiConfig Source { get; set; } = new();
    public ApiConfig Sink { get; set; } = new();

    public ApiFieldConfig[] Mappings { get; set; } = [];
}