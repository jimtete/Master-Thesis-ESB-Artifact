namespace OlympusServiceBusApplication.Models.Contracts;

public class ContractFieldMappingModel
{
    public string SourceField { get; set; } = string.Empty;
    public string TargetField { get; set; } = string.Empty;
    public string Transformation { get; set; } = "Direct";
    public string Separator { get; set; } = " ";
}