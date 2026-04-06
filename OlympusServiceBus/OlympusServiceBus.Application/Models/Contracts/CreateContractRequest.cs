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

    public string ListenerPath { get; set; } = "/incoming";
    public string ListenerMethod { get; set; } = "POST";

    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = "csv";

    public ScheduleEditorRequest? Schedule { get; set; }

    public List<ContractFieldMappingModel> Mappings { get; set; } = [];
}