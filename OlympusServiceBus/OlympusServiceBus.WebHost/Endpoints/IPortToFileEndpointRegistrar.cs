using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.WebHost.Endpoints;

public interface IPortToFileEndpointRegistrar
{
    void Register(WebApplication app, List<PortToFileContract> contracts);
}