namespace OlympusServiceBusApplication.Models.Contracts;

public class CreateContractRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ContractType { get; set; } = "ApiToApi";

    // API source
    public string SourceEndpoint { get; set; } = string.Empty;
    public string SourceMethod { get; set; } = "GET";

    // Port/listener source
    public string ListenerPath { get; set; } = "/";
    public string ListenerMethod { get; set; } = "POST";

    // File source
    public string SourceDirectory { get; set; } = string.Empty;
    public string SourceSearchPattern { get; set; } = "*.csv";
    public bool SourceIncludeSubdirectories { get; set; }
    public string SourceProcessedDirectory { get; set; } = string.Empty;
    public string SourceErrorDirectory { get; set; } = string.Empty;

    // API sink
    public string SinkEndpoint { get; set; } = string.Empty;
    public string SinkMethod { get; set; } = "POST";

    // File sink
    public string SinkDirectory { get; set; } = string.Empty;
    public string SinkFileExtension { get; set; } = "csv";

    public string BusinessKeyField { get; set; } = "id";

    public ScheduleEditorRequest? Schedule { get; set; }

    public List<ContractFieldMappingModel> Mappings { get; set; } = [];
}
