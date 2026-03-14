using System.Text.Json.Nodes;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Execution.AntiContracts;

public static class AntiContractExecutionContextFactory
{
    public static AntiContractExecutionContext CreateSuccess(
        ContractBase sourceContract,
        string businessKey,
        JsonObject? originalPayload,
        JsonObject? transformedPayload,
        JsonObject? responsePayload = null,
        Dictionary<string, string>? correlationValues = null)
    {
        ArgumentNullException.ThrowIfNull(sourceContract);

        return new AntiContractExecutionContext
        {
            SourceContractId = sourceContract.ContractId,
            SourceContractName = sourceContract.Name,
            BusinessKey = businessKey,
            ExecutionStatus = "Success",
            OriginalPayload = originalPayload,
            TransformedPayload = transformedPayload,
            ResponsePayload = responsePayload,
            CorrelationValues = correlationValues ?? new Dictionary<string, string>(),
            CompletedAtUtc = DateTime.UtcNow
        };
    }

    public static AntiContractExecutionContext CreateFailure(
        ContractBase sourceContract,
        string businessKey,
        string? errorMessage,
        string? errorCode = null,
        JsonObject? originalPayload = null,
        JsonObject? transformedPayload = null,
        JsonObject? responsePayload = null,
        Dictionary<string, string>? correlationValues = null)
    {
        ArgumentNullException.ThrowIfNull(sourceContract);

        return new AntiContractExecutionContext
        {
            SourceContractId = sourceContract.ContractId,
            SourceContractName = sourceContract.Name,
            BusinessKey = businessKey,
            ExecutionStatus = "Failed",
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            OriginalPayload = originalPayload,
            TransformedPayload = transformedPayload,
            ResponsePayload = responsePayload,
            CorrelationValues = correlationValues ?? new Dictionary<string, string>(),
            CompletedAtUtc = DateTime.UtcNow
        };
    }
}