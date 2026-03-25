namespace OlympusServiceBusApplication.Models.Contracts;

public class ContractFieldMappingModel
{
    public List<string> SourceFields { get; set; } = [];
    public List<string> TargetFields { get; set; } = [];
    public string Transformation { get; set; } = "Direct";
    public string Separator { get; set; } = " ";

    public string SourceFieldsText
    {
        get => string.Join(", ", SourceFields);
        set => SourceFields = SplitFields(value);
    }

    public string TargetFieldsText
    {
        get => string.Join(", ", TargetFields);
        set => TargetFields = SplitFields(value);
    }

    private static List<string> SplitFields(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }
}