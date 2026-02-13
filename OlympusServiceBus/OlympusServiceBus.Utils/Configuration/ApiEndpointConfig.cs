namespace OlympusServiceBus.Utils.Configuration;

public class ApiEndpointConfig
{
    public ApiRequestConfig Request { get; set; } = new();
    public ApiResponseConfig Response { get; set; } = new();
}