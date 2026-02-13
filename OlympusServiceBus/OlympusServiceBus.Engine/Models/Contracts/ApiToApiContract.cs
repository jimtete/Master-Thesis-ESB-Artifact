using OlympusServiceBus.Engine.Models.Configuration;

namespace OlympusServiceBus.Engine.Models.Contracts;

public class ApiToApiContract : ContractBase
{
    public ApiConfig Source { get; set; }
    public ApiConfig Sink { get; set; }
    public ApiFieldConfig[] Mappings { get; set; }
    
}