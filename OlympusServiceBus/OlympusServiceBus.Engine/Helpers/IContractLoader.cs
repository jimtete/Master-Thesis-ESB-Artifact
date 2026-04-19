using OlympusServiceBus.Utils.Contracts;
using OlympusServiceBus.Utils.Contracts.FeedbackContracts;

namespace OlympusServiceBus.Engine.Helpers;

public interface IContractLoader
{
    List<ContractBase> LoadAllContracts(string contractDirectory);
    List<FeedbackContractBase> LoadAllFeedbackContracts(string contractDirectory);
}