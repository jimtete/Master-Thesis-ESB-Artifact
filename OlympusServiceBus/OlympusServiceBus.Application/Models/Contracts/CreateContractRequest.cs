namespace OlympusServiceBusApplication.Models.Contracts;

public class CreateContractRequest
{
    public string Name { get; set; } = string.Empty;
    public string ContractType { get; set; } = "ApiToApi";

    public string SourceEndpoint { get; set; } = string.Empty;
    public string SourceMethod { get; set; } = "GET";

    public string SinkEndpoint { get; set; } = string.Empty;
    public string SinkMethod { get; set; } = "POST";

    public string BusinessKeyField { get; set; } = "id";

    public List<ContractFieldMappingModel> Mappings { get; set; } = [];
}