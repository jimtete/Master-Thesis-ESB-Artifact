namespace OlympusServiceBus.Utils.Configuration;

public class ApiFieldConfig
{
    public string SourceFieldName { get; set; }
    public string SinkFieldName { get; set; }
    public TransformationType TransformationType { get; set; } = TransformationType.Direct;
    public string Separator { get; set; } = " ";
    public string[] SourceFields { get; set; }
    public string[] SinkFields { get; set; }
}