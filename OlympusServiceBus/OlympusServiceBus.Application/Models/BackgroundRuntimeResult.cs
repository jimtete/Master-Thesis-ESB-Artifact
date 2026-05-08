namespace OlympusServiceBusApplication.Models;

public sealed record BackgroundRuntimeResult(
    bool Success,
    string Message,
    bool IsUnsupported = false);
