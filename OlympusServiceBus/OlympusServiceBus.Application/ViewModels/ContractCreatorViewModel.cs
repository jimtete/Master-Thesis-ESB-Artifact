using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using OlympusServiceBusApplication.Commands;
using OlympusServiceBusApplication.Models.Contracts;

namespace OlympusServiceBusApplication.ViewModels;

public class ContractCreatorViewModel : INotifyPropertyChanged
{
    private const int MaxDescriptionLength = 1000;

    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _contractType = "ApiToApi";

    private string _sourceEndpoint = string.Empty;
    private string _sourceMethod = "GET";

    private string _sinkEndpoint = string.Empty;
    private string _sinkMethod = "POST";

    private string _businessKeyField = "id";

    private string _listenerPath = "/incoming";
    private string _listenerMethod = "POST";

    private string _sourceDirectory = string.Empty;
    private string _sourceSearchPattern = "*.csv";
    private bool _sourceIncludeSubdirectories;
    private string _sourceProcessedDirectory = string.Empty;
    private string _sourceErrorDirectory = string.Empty;

    private string _sinkDirectory = string.Empty;
    private string _sinkFileExtension = "csv";

    private ScheduleEditorRequest? _schedule;

    private bool _isEditMode;
    private string? _selectedContractFilePath;

    public string Name
    {
        get => _name;
        set
        {
            var sanitizedName = RemoveWhitespace(value);

            if (_name == sanitizedName) return;
            _name = sanitizedName;
            OnPropertyChanged();
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            var truncatedDescription = TruncateDescription(value);

            if (_description == truncatedDescription) return;
            _description = truncatedDescription;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DescriptionCharacterCountText));
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
            OnContractTypeChanged();

            if (UsesPortSource)
            {
                Schedule = null;
            }
        }
    }

    public bool IsApiToApi => string.Equals(ContractType, "ApiToApi", StringComparison.OrdinalIgnoreCase);
    public bool IsApiToFile => string.Equals(ContractType, "ApiToFile", StringComparison.OrdinalIgnoreCase);
    public bool IsPortToApi => string.Equals(ContractType, "PortToApi", StringComparison.OrdinalIgnoreCase);
    public bool IsPortToFile => string.Equals(ContractType, "PortToFile", StringComparison.OrdinalIgnoreCase);
    public bool IsFileToApi => string.Equals(ContractType, "FileToApi", StringComparison.OrdinalIgnoreCase);
    public bool IsFileToFile => string.Equals(ContractType, "FileToFile", StringComparison.OrdinalIgnoreCase);

    public bool UsesApiSource => IsApiToApi || IsApiToFile;
    public bool UsesPortSource => IsPortToApi || IsPortToFile;
    public bool UsesFileSource => IsFileToApi || IsFileToFile;
    public bool UsesApiSink => IsApiToApi || IsPortToApi || IsFileToApi;
    public bool UsesFileSink => IsApiToFile || IsPortToFile || IsFileToFile;
    public bool SupportsBusinessKey => true;
    public bool SupportsScheduling => UsesApiSource || UsesFileSource;

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

    public string SourceDirectory
    {
        get => _sourceDirectory;
        set
        {
            if (_sourceDirectory == value) return;
            _sourceDirectory = value;
            OnPropertyChanged();
        }
    }

    public string SourceSearchPattern
    {
        get => _sourceSearchPattern;
        set
        {
            if (_sourceSearchPattern == value) return;
            _sourceSearchPattern = value;
            OnPropertyChanged();
        }
    }

    public bool SourceIncludeSubdirectories
    {
        get => _sourceIncludeSubdirectories;
        set
        {
            if (_sourceIncludeSubdirectories == value) return;
            _sourceIncludeSubdirectories = value;
            OnPropertyChanged();
        }
    }

    public string SourceProcessedDirectory
    {
        get => _sourceProcessedDirectory;
        set
        {
            if (_sourceProcessedDirectory == value) return;
            _sourceProcessedDirectory = value;
            OnPropertyChanged();
        }
    }

    public string SourceErrorDirectory
    {
        get => _sourceErrorDirectory;
        set
        {
            if (_sourceErrorDirectory == value) return;
            _sourceErrorDirectory = value;
            OnPropertyChanged();
        }
    }

    public string SinkDirectory
    {
        get => _sinkDirectory;
        set
        {
            if (_sinkDirectory == value) return;
            _sinkDirectory = value;
            OnPropertyChanged();
        }
    }

    public string SinkFileExtension
    {
        get => _sinkFileExtension;
        set
        {
            if (_sinkFileExtension == value) return;
            _sinkFileExtension = value;
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
    public string DescriptionCharacterCountText => $"{Description.Length}/{MaxDescriptionLength}";

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
            Description = Description.Trim(),
            ContractType = ContractType.Trim(),

            SourceEndpoint = SourceEndpoint.Trim(),
            SourceMethod = SourceMethod.Trim(),

            SinkEndpoint = SinkEndpoint.Trim(),
            SinkMethod = SinkMethod.Trim(),

            BusinessKeyField = BusinessKeyField.Trim(),

            ListenerPath = ListenerPath.Trim(),
            ListenerMethod = ListenerMethod.Trim(),

            SourceDirectory = SourceDirectory.Trim(),
            SourceSearchPattern = SourceSearchPattern.Trim(),
            SourceIncludeSubdirectories = SourceIncludeSubdirectories,
            SourceProcessedDirectory = SourceProcessedDirectory.Trim(),
            SourceErrorDirectory = SourceErrorDirectory.Trim(),

            SinkDirectory = SinkDirectory.Trim(),
            SinkFileExtension = SinkFileExtension.Trim(),

            Schedule = SupportsScheduling ? Schedule : null,

            Mappings = Mappings.ToList(),
        };
    }

    public void LoadFromRequest(CreateContractRequest request, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        Name = request.Name;
        Description = request.Description;
        ContractType = string.IsNullOrWhiteSpace(request.ContractType) ? "ApiToApi" : request.ContractType;

        SourceEndpoint = request.SourceEndpoint;
        SourceMethod = string.IsNullOrWhiteSpace(request.SourceMethod) ? "GET" : request.SourceMethod;

        SinkEndpoint = request.SinkEndpoint;
        SinkMethod = string.IsNullOrWhiteSpace(request.SinkMethod) ? "POST" : request.SinkMethod;

        BusinessKeyField = string.IsNullOrWhiteSpace(request.BusinessKeyField) ? "id" : request.BusinessKeyField;

        ListenerPath = string.IsNullOrWhiteSpace(request.ListenerPath) ? "/incoming" : request.ListenerPath;
        ListenerMethod = string.IsNullOrWhiteSpace(request.ListenerMethod) ? "POST" : request.ListenerMethod;

        SourceDirectory = request.SourceDirectory;
        SourceSearchPattern = string.IsNullOrWhiteSpace(request.SourceSearchPattern)
            ? "*.csv"
            : request.SourceSearchPattern;
        SourceIncludeSubdirectories = request.SourceIncludeSubdirectories;
        SourceProcessedDirectory = request.SourceProcessedDirectory;
        SourceErrorDirectory = request.SourceErrorDirectory;

        SinkDirectory = request.SinkDirectory;
        SinkFileExtension = string.IsNullOrWhiteSpace(request.SinkFileExtension)
            ? "csv"
            : request.SinkFileExtension;

        Schedule = UsesPortSource ? null : request.Schedule;

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
                        : mapping.Separator,
                    Expression = mapping.Expression ?? string.Empty
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
        Description = string.Empty;
        ContractType = "ApiToApi";

        SourceEndpoint = string.Empty;
        SourceMethod = "GET";

        SinkEndpoint = string.Empty;
        SinkMethod = "POST";

        BusinessKeyField = "id";

        ListenerPath = "/incoming";
        ListenerMethod = "POST";

        SourceDirectory = string.Empty;
        SourceSearchPattern = "*.csv";
        SourceIncludeSubdirectories = false;
        SourceProcessedDirectory = string.Empty;
        SourceErrorDirectory = string.Empty;

        SinkDirectory = string.Empty;
        SinkFileExtension = "csv";

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

    private void OnContractTypeChanged()
    {
        OnPropertyChanged(nameof(IsApiToApi));
        OnPropertyChanged(nameof(IsApiToFile));
        OnPropertyChanged(nameof(IsPortToApi));
        OnPropertyChanged(nameof(IsPortToFile));
        OnPropertyChanged(nameof(IsFileToApi));
        OnPropertyChanged(nameof(IsFileToFile));
        OnPropertyChanged(nameof(UsesApiSource));
        OnPropertyChanged(nameof(UsesPortSource));
        OnPropertyChanged(nameof(UsesFileSource));
        OnPropertyChanged(nameof(UsesApiSink));
        OnPropertyChanged(nameof(UsesFileSink));
        OnPropertyChanged(nameof(SupportsBusinessKey));
        OnPropertyChanged(nameof(SupportsScheduling));
    }

    private static string RemoveWhitespace(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string TruncateDescription(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= MaxDescriptionLength
            ? value
            : value[..MaxDescriptionLength];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
