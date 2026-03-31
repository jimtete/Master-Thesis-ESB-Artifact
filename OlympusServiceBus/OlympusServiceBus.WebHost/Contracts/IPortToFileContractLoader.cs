using OlympusServiceBus.Utils.Contracts;

namespace OlympusServiceBus.WebHost.Contracts;

public interface IPortToFileContractLoader
{
    List<PortToFileContract> Load(string? rootPath);
}