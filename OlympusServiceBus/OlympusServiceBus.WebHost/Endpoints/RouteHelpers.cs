namespace OlympusServiceBus.WebHost.Endpoints;

public static class RouteHelpers
{
    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";
        return path.StartsWith('/') ? path : "/" + path;
    }
}