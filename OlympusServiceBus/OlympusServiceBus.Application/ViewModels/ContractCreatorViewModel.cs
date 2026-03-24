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
    private bool _isEditMode;
    private string? _selectedContractFilePath;

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

    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (_isEditMode == value) return;
            _isEditMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormTitle));
            OnPropertyChanged(nameof(SaveButtonText));
        }
    }

    public string? SelectedContractFilePath
    {
        get => _selectedContractFilePath;
        set
        {
            if (_selectedContractFilePath == value) return;
            _selectedContractFilePath = value;
            OnPropertyChanged();
        }
    }

    public string FormTitle => IsEditMode ? "Edit Contract" : "Create Contract";
    public string SaveButtonText => IsEditMode ? "Save Contract" : "Create Contract";

    public ObservableCollection<ContractFieldMappingModel> Mappings { get; } = [];

    public ICommand AddMappingCommand { get; }
    public ICommand RemoveMappingCommand { get; }

    public ContractCreatorViewModel()
    {
        AddMappingCommand = new RelayCommand(AddMapping);
        RemoveMappingCommand = new RelayCommandT<ContractFieldMappingModel>(RemoveMapping);

        ResetToDefaults();
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

    public void LoadFromRequest(CreateContractRequest request, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        Name = request.Name;
        ContractType = string.IsNullOrWhiteSpace(request.ContractType) ? "ApiToApi" : request.ContractType;
        SourceEndpoint = request.SourceEndpoint;
        SourceMethod = string.IsNullOrWhiteSpace(request.SourceMethod) ? "GET" : request.SourceMethod;
        SinkEndpoint = request.SinkEndpoint;
        SinkMethod = string.IsNullOrWhiteSpace(request.SinkMethod) ? "POST" : request.SinkMethod;
        BusinessKeyField = string.IsNullOrWhiteSpace(request.BusinessKeyField) ? "id" : request.BusinessKeyField;

        Mappings.Clear();

        if (request.Mappings is { Count: > 0 })
        {
            foreach (var mapping in request.Mappings)
            {
                Mappings.Add(new ContractFieldMappingModel
                {
                    SourceField = mapping.SourceField,
                    TargetField = mapping.TargetField,
                    Transformation = string.IsNullOrWhiteSpace(mapping.Transformation)
                        ? "Direct"
                        : mapping.Transformation
                });
            }
        }
        else
        {
            Mappings.Add(new ContractFieldMappingModel
            {
                SourceField = "id",
                TargetField = "id",
                Transformation = "Direct"
            });
        }

        SelectedContractFilePath = filePath;
        IsEditMode = true;
    }

    public void Clear()
    {
        ResetToDefaults();
        SelectedContractFilePath = null;
        IsEditMode = false;
    }

    private void ResetToDefaults()
    {
        Name = string.Empty;
        ContractType = "ApiToApi";
        SourceEndpoint = string.Empty;
        SourceMethod = "GET";
        SinkEndpoint = string.Empty;
        SinkMethod = "POST";
        BusinessKeyField = "id";

        Mappings.Clear();
        Mappings.Add(new ContractFieldMappingModel
        {
            SourceField = "id",
            TargetField = "id",
            Transformation = "Direct"
        });
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

        if (Mappings.Count == 0)
        {
            Mappings.Add(new ContractFieldMappingModel
            {
                Transformation = "Direct"
            });
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}