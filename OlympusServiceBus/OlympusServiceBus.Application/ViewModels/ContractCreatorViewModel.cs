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

    private string _listenerPath = "/incoming";
    private string _listenerMethod = "POST";

    private string _filePath = string.Empty;
    private string _fileType = "csv";
    private ScheduleEditorRequest? _schedule;

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
            OnPropertyChanged(nameof(IsApiToApi));
            OnPropertyChanged(nameof(IsPortToApi));
            OnPropertyChanged(nameof(IsFileToApi));
            OnPropertyChanged(nameof(SupportsBusinessKey));
            OnPropertyChanged(nameof(SupportsScheduling));

            if (IsPortToApi)
            {
                Schedule = null;
            }
        }
    }

    public bool IsApiToApi => string.Equals(ContractType, "ApiToApi", StringComparison.OrdinalIgnoreCase);
    public bool IsPortToApi => string.Equals(ContractType, "PortToApi", StringComparison.OrdinalIgnoreCase);
    public bool IsFileToApi => string.Equals(ContractType, "FileToApi", StringComparison.OrdinalIgnoreCase);
    public bool SupportsBusinessKey => IsApiToApi || IsPortToApi;
    public bool SupportsScheduling => IsApiToApi || IsFileToApi;

    public bool HasSchedule => Schedule is not null;

    public string ScheduleSummary
    {
        get
        {
            if (Schedule is null)
            {
                return "No schedule configured.";
            }

            return Schedule.Mode switch
            {
                "Manual" => "Manual execution",
                "AdHoc" => Schedule.RunAt is not null
                    ? $"AdHoc - {Schedule.RunAt.Value:yyyy-MM-dd HH:mm zzz}"
                    : "AdHoc - missing date/time",
                "Interval" => $"Every {Schedule.IntervalValue} {Schedule.IntervalUnit}",
                "Recurring" => string.IsNullOrWhiteSpace(Schedule.CronExpression)
                    ? "CRON - missing expression"
                    : $"CRON - {Schedule.CronExpression}",
                _ => $"Unknown schedule mode: {Schedule.Mode}"
            };
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

    public string ListenerPath
    {
        get => _listenerPath;
        set
        {
            if (_listenerPath == value) return;
            _listenerPath = value;
            OnPropertyChanged();
        }
    }

    public string ListenerMethod
    {
        get => _listenerMethod;
        set
        {
            if (_listenerMethod == value) return;
            _listenerMethod = value;
            OnPropertyChanged();
        }
    }

    public string FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath == value) return;
            _filePath = value;
            OnPropertyChanged();
        }
    }

    public string FileType
    {
        get => _fileType;
        set
        {
            if (_fileType == value) return;
            _fileType = value;
            OnPropertyChanged();
        }
    }

    public ScheduleEditorRequest? Schedule
    {
        get => _schedule;
        set
        {
            if (_schedule == value) return;
            _schedule = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSchedule));
            OnPropertyChanged(nameof(ScheduleSummary));
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

            ListenerPath = ListenerPath.Trim(),
            ListenerMethod = ListenerMethod.Trim(),

            FilePath = FilePath.Trim(),
            FileType = FileType.Trim(),

            Schedule = Schedule,

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

        ListenerPath = string.IsNullOrWhiteSpace(request.ListenerPath) ? "/incoming" : request.ListenerPath;
        ListenerMethod = string.IsNullOrWhiteSpace(request.ListenerMethod) ? "POST" : request.ListenerMethod;

        FilePath = request.FilePath;
        FileType = string.IsNullOrWhiteSpace(request.FileType) ? "csv" : request.FileType;

        Schedule = request.Schedule;

        Mappings.Clear();

        if (request.Mappings is { Count: > 0 })
        {
            foreach (var mapping in request.Mappings)
            {
                Mappings.Add(new ContractFieldMappingModel
                {
                    SourceFields = mapping.SourceFields?.ToList() ?? [],
                    TargetFields = mapping.TargetFields?.ToList() ?? [],
                    Transformation = string.IsNullOrWhiteSpace(mapping.Transformation)
                        ? "Direct"
                        : mapping.Transformation,
                    Separator = string.IsNullOrWhiteSpace(mapping.Separator)
                        ? " "
                        : mapping.Separator
                });
            }
        }
        else
        {
            Mappings.Add(new ContractFieldMappingModel
            {
                SourceFields = ["id"],
                TargetFields = ["id"],
                Transformation = "Direct",
                Separator = " "
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

        ListenerPath = "/incoming";
        ListenerMethod = "POST";

        FilePath = string.Empty;
        FileType = "csv";

        Schedule = null;

        Mappings.Clear();
        Mappings.Add(new ContractFieldMappingModel
        {
            SourceFields = ["id"],
            TargetFields = ["id"],
            Transformation = "Direct",
            Separator = " "
        });
    }

    private void AddMapping()
    {
        Mappings.Add(new ContractFieldMappingModel
        {
            Transformation = "Direct",
            Separator = " "
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
                Transformation = "Direct",
                Separator = " "
            });
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}