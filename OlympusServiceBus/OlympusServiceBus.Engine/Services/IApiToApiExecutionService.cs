using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.Engine.Services;

public interface IApiToApiExecutionService
{
    Task ExecuteAsync(ApiToApiContract contract, CancellationToken cancellationToken);
}