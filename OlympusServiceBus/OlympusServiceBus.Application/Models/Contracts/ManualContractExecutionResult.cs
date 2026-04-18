namespace OlympusServiceBusApplication.Models.Contracts;

public sealed record ManualContractExecutionResult(
    bool Success,
    string Message);
