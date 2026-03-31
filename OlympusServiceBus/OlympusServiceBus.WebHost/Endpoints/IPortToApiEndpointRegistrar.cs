using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.WebHost.Endpoints;

public interface IPortToApiEndpointRegistrar
{
    void Register(WebApplication app, List<PortToApiContract> contracts);
}