using OlympusServiceBus.Utils.Configuration;

namespace OlympusServiceBus.Utils.Contracts;

public class ApiToApiContract : ContractBase
{
    public ApiConfig Source { get; set; }
    public ApiConfig Sink { get; set; }
    public ApiFieldConfig[] Mappings { get; set; }
    
}