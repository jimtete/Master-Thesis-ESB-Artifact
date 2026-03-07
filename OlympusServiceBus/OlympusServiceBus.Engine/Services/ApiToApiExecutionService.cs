using OlympusServiceBus.Engine.Execution;
using OlympusServiceBus.RuntimeState.Models;
using OlympusServiceBus.RuntimeState.Services;
using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Services;

public class ApiToApiExecutionService : IApiToApiExecutionService
{
    private readonly ApiToApiExecutor _executor;
    private readonly IContractExecutionStateService _executionStateService;

    public ApiToApiExecutionService(ApiToApiExecutor executor, IContractExecutionStateService executionStateService)
    {
        _executor = executor;
        _executionStateService = executionStateService;
    }

    public async Task ExecuteAsync(ApiToApiContract contract, CancellationToken cancellationToken)
    {
        await _executionStateService.SaveAsync(new ContractExecutionStateEntity
        {
            ContractId = contract.ContractId,
            ContractName = contract.Name,
            LastRunStartedAt = DateTimeOffset.UtcNow,
            LastRunStatus = "Running",
            UpdatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _executor.ExecuteOnce(contract, cancellationToken);

        await _executionStateService.SaveAsync(new ContractExecutionStateEntity
        {
            ContractId = contract.ContractId,
            ContractName = contract.Name,
            LastRunCompletedAt = DateTimeOffset.UtcNow,
            LastRunStatus = "Completed",
            UpdatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);    }
}