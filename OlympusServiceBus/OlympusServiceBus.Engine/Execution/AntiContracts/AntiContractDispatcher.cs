namespace OlympusServiceBus.Engine.Execution.AntiContracts;

public sealed class AntiContractDispatcher
{
    private readonly IAntiContractRegistry _antiContractRegistry;
    private readonly AntiContractExecutionService _antiContractExecutionService;
    private readonly ILogger<AntiContractDispatcher> _logger;

    public AntiContractDispatcher(
        IAntiContractRegistry antiContractRegistry, 
        AntiContractExecutionService antiContractExecutionService, 
        ILogger<AntiContractDispatcher> logger)
    {
        _antiContractRegistry = antiContractRegistry;
        _antiContractExecutionService = antiContractExecutionService;
        _logger = logger;
    }
    
    public async Task DispatchAsync(
        string sourceContractId,
        AntiContractExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceContractId))
            throw new ArgumentException("Source contract id is required.", nameof(sourceContractId));

        var antiContracts = _antiContractRegistry.GetBySourceContractId(sourceContractId);

        if (antiContracts.Count == 0)
        {
            _logger.LogDebug(
                "No anti-contracts registered for source contract {SourceContractId}",
                sourceContractId);

            return;
        }

        _logger.LogDebug(
            "Found {Count} anti-contract(s) for source contract {SourceContractId}",
            antiContracts.Count,
            sourceContractId);

        foreach (var antiContract in antiContracts)
        {
            if (!AntiContractTriggerEvaluator.ShouldExecute(antiContract, context.ExecutionStatus))
            {
                _logger.LogDebug(
                    "Skipping anti-contract {AntiContractId} for source contract {SourceContractId} because trigger mode {TriggerMode} does not match execution status {ExecutionStatus}",
                    antiContract.ContractId,
                    sourceContractId,
                    antiContract.TriggerMode,
                    context.ExecutionStatus);

                continue;
            }

            try
            {
                _logger.LogInformation(
                    "Dispatching anti-contract {AntiContractId} for source contract {SourceContractId} with execution status {ExecutionStatus}",
                    antiContract.ContractId,
                    sourceContractId,
                    context.ExecutionStatus);

                await _antiContractExecutionService.ExecuteAsync(
                    antiContract,
                    context,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to dispatch anti-contract {AntiContractId} for source contract {SourceContractId}",
                    antiContract.ContractId,
                    sourceContractId);
            }
        }
    }
}