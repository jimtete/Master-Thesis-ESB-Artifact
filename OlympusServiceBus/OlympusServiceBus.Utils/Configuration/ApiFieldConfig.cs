namespace OlympusServiceBus.Utils.Configuration;

public class ApiFieldConfig
{
    public SourceField SourceFieldName { get; set; }
    public SinkField SinkFieldName { get; set; }

    public TransformationType TransformationType { get; set; } = TransformationType.Direct;

    public string Separator { get; set; } = " ";

    public SourceField[]? SourceFields { get; set; }
    public SinkField[]? SinkFields { get; set; }

    // Used when TransformationType == Expression.
    // Binding rule:
    // SourceFields[0] -> $i_0
    // SourceFields[1] -> $i_1
    // SinkFields[0]   -> $o_0
    // SinkFields[1]   -> $o_1
    public string? Expression { get; set; }
}