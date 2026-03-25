namespace OlympusServiceBus.Utils.Configuration;

public class ApiFieldConfig
{
    public SourceField SourceFieldName { get; set; }
    public SinkField SinkFieldName { get; set; }
    public TransformationType TransformationType { get; set; } = TransformationType.Direct;
    public string Separator { get; set; } = " ";
    public SourceField[]? SourceFields { get; set; }
    public SinkField[]? SinkFields { get; set; }
}