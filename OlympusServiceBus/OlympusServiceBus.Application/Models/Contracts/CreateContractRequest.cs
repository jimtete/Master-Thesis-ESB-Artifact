namespace OlympusServiceBusApplication.Models.Contracts;

public class CreateContractRequest
{
    public string Name { get; set; } = string.Empty;
    public string ContractType { get; set; } = "ApiToApi";

    // ApiToApi
    public string SourceEndpoint { get; set; } = string.Empty;
    public string SourceMethod { get; set; } = "GET";

    // Shared sink for all 3 contract types
    public string SinkEndpoint { get; set; } = string.Empty;
    public string SinkMethod { get; set; } = "POST";

    // ApiToApi / general mapping support
    public string BusinessKeyField { get; set; } = "id";

    // PortToApi
    public string ListenerPath { get; set; } = "/incoming";
    public string ListenerMethod { get; set; } = "POST";

    // FileToApi
    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = "Csv";

    public List<ContractFieldMappingModel> Mappings { get; set; } = [];
}