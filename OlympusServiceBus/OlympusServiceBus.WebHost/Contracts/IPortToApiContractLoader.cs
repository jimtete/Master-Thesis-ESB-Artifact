using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.WebHost.Contracts;

public interface IPortToApiContractLoader
{
    List<PortToApiContract> Load(string? rootPath);
}