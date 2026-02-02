namespace OlympusServiceBus.Engine.Models.Configuration;

public class ApiEndpointConfig
{
    public ApiRequestConfig Request { get; set; } = new();
    public ApiResponseConfig Response { get; set; } = new();
}