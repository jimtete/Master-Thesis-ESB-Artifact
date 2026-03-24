using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OlympusServiceBusApplication.Commands;
using OlympusServiceBusApplication.Models.Contracts;

namespace OlympusServiceBusApplication.ViewModels;

public class ContractCreatorViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _contractType = "ApiToApi";
    private string _sourceEndpoint = string.Empty;
    private string _sourceMethod = "GET";
    private string _sinkEndpoint = string.Empty;
    private string _sinkMethod = "POST";
    private string _businessKeyField = "id";
    
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    public string ContractType
    {
        get => _contractType;
        set
        {
            if (_contractType == value) return;
            _contractType = value;
            OnPropertyChanged();
        }
    }

    public string SourceEndpoint
    {
        get => _sourceEndpoint;
        set
        {
            if (_sourceEndpoint == value) return;
            _sourceEndpoint = value;
            OnPropertyChanged();
        }
    }

    public string SourceMethod
    {
        get => _sourceMethod;
        set
        {
            if (_sourceMethod == value) return;
            _sourceMethod = value;
            OnPropertyChanged();
        }
    }

    public string SinkEndpoint
    {
        get => _sinkEndpoint;
        set
        {
            if (_sinkEndpoint == value) return;
            _sinkEndpoint = value;
            OnPropertyChanged();
        }
    }

    public string SinkMethod
    {
        get => _sinkMethod;
        set
        {
            if (_sinkMethod == value) return;
            _sinkMethod = value;
            OnPropertyChanged();
        }
    }

    public string BusinessKeyField
    {
        get => _businessKeyField;
        set
        {
            if (_businessKeyField == value) return;
            _businessKeyField = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ContractFieldMappingModel> Mappings { get; } = [];
    
    public ICommand AddMappingCommand { get; }
    public ICommand RemoveMappingCommand { get; }

    public ContractCreatorViewModel()
    {
        AddMappingCommand = new RelayCommand(AddMapping);
        RemoveMappingCommand = new RelayCommandT<ContractFieldMappingModel>(RemoveMapping);
        
        Mappings.Add(new ContractFieldMappingModel
        {
            SourceField = "id",
            TargetField = "id",
            Transformation = "Direct"
        });
    }

    public CreateContractRequest BuildRequest()
    {
        return new CreateContractRequest
        {
            Name = Name.Trim(),
            ContractType = ContractType.Trim(),
            SourceEndpoint = SourceEndpoint.Trim(),
            SourceMethod = SourceMethod.Trim(),
            SinkEndpoint = SinkEndpoint.Trim(),
            SinkMethod = SinkMethod.Trim(),
            BusinessKeyField = BusinessKeyField.Trim(),
            Mappings = Mappings.ToList(),
        };
    }
    
    private void AddMapping()
    {
        Mappings.Add(new ContractFieldMappingModel
        {
            Transformation = "Direct",
        });
    }
    
    private void RemoveMapping(ContractFieldMappingModel? mapping)
    {
        if (mapping == null)
        {
            return;
        }

        Mappings.Remove(mapping);
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}