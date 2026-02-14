using OlympusServiceBus.Utils.Configuration;

namespace OlympusServiceBus.Utils.Contracts;

public class PortToApiContract : ContractBase
{
    public ListenerConfig Listener { get; set; } = new();
    public ApiConfig Sink { get; set; }
    
    public PortToApiRequest? Request { get; set; }
    
    public ApiFieldConfig[] Mappings { get; set; } = Array.Empty<ApiFieldConfig>();
}

public class ListenerConfig
{
    public string Path { get; set; } = "/";
    public string Method { get; set; } = "POST";
}

public sealed class PortToApiRequest
{
    public PortToApiRequestField[] Fields { get; set; } =  Array.Empty<PortToApiRequestField>();
}

public sealed class PortToApiRequestField
{
    public string FieldName { get; set; } = "";
    public JsonFieldType Type { get; set; } = JsonFieldType.String;
    public string? Format { get; set; }
    public bool Required { get; set; } = true;
    public object? Example { get; set; }
}

public enum JsonFieldType
{
    String,
    Integer,
    Number,
    Boolean,
    Object,
    Array
}