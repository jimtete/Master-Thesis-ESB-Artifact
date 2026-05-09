using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Services;

public interface IApiToFileExecutionService
{
    Task ExecuteAsync(ApiToFileContract contract, string triggerType, CancellationToken cancellationToken);
}
