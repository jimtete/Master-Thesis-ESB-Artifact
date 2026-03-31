using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Services;

public interface IApiToFileExecutionService
{
    Task ExecuteAsync(ApiToFileContract contract, CancellationToken cancellationToken);
}