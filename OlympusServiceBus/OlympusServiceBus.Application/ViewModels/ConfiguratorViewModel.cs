using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using OlympusServiceBusApplication.Commands;
using OlympusServiceBusApplication.Models;
using OlympusServiceBusApplication.Models.Contracts;
using OlympusServiceBusApplication.Services.ContractsService;

namespace OlympusServiceBusApplication.ViewModels;

public class ConfiguratorViewModel : INotifyPropertyChanged
{
    private readonly IContractsWorkspaceService _contractsWorkspaceService;
    private readonly IContractsExplorerService _contractsExplorerService;

    private string _contractsDirectoryPath = string.Empty;
    private FileExplorerNode? _rootNode;
    private FileExplorerNode? _selectedNode;
    private string _newDirectoryName = string.Empty;
    private string _statusMessage = "Ready.";

    public string ContractsDirectoryPath
    {
        get => _contractsDirectoryPath;
        set
        {
            if (_contractsDirectoryPath == value)
            {
                return;
            }

            _contractsDirectoryPath = value;
            OnPropertyChanged();
        }
    }

    public FileExplorerNode? RootNode
    {
        get => _rootNode;
        set
        {
            if (_rootNode == value)
            {
                return;
            }

            _rootNode = value;
            OnPropertyChanged();
        }
    }

    public FileExplorerNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode == value)
            {
                return;
            }

            _selectedNode = value;
            OnPropertyChanged();
        }
    }

    public string NewDirectoryName
    {
        get => _newDirectoryName;
        set
        {
            if (_newDirectoryName == value)
            {
                return;
            }

            _newDirectoryName = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public ICommand CreateDirectoryCommand { get; }
    public ICommand CreateContractCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ClearContractSelectionCommand { get; }

    public ContractCreatorViewModel ContractCreator { get; }

    public ConfiguratorViewModel(
        IContractsWorkspaceService contractsWorkspaceService,
        IContractsExplorerService contractsExplorerService)
    {
        _contractsWorkspaceService = contractsWorkspaceService;
        _contractsExplorerService = contractsExplorerService;

        CreateDirectoryCommand = new AsyncRelayCommand(CreateDirectoryAsync);
        CreateContractCommand = new AsyncRelayCommand(CreateContractAsync);
        RefreshCommand = new AsyncRelayCommand(ReloadTreeAsync);
        ClearContractSelectionCommand = new RelayCommand(ClearContractSelection);

        ContractCreator = new ContractCreatorViewModel();
    }

    public async Task LoadAsync()
    {
        ContractsDirectoryPath = await _contractsWorkspaceService.EnsureContractsDirectoryAsync();
        await ReloadTreeAsync();
        StatusMessage = "Contracts workspace loaded.";
    }

    private async Task CreateDirectoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDirectoryName))
        {
            StatusMessage = "Please enter a folder name.";
            return;
        }

        var parentPath = ResolveTargetDirectoryPath();

        await _contractsExplorerService.CreateDirectoryAsync(parentPath, NewDirectoryName);
        var createdDirectoryName = NewDirectoryName.Trim();

        NewDirectoryName = string.Empty;
        await ReloadTreeAsync();

        StatusMessage = $"Folder '{createdDirectoryName}' created successfully.";
    }

    private async Task CreateContractAsync()
    {
        var request = ContractCreator.BuildRequest();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            StatusMessage = "Please enter a contract name.";
            return;
        }

        if (request.Schedule is null)
        {
            StatusMessage = "Please configure scheduling before saving the contract.";
            return;
        }

        var parentPath = ResolveTargetDirectoryPath();
        var previousFilePath = ContractCreator.SelectedContractFilePath;

        await _contractsExplorerService.CreateContractFileAsync(
            parentPath,
            request,
            previousFilePath);

        var createdOrUpdatedContractName = request.Name.Trim();
        var newFilePath = Path.Combine(parentPath, $"{createdOrUpdatedContractName}.json");

        await ReloadTreeAsync();

        ContractCreator.SelectedContractFilePath = newFilePath;
        ContractCreator.IsEditMode = true;

        StatusMessage = previousFilePath is not null
            ? $"Contract '{createdOrUpdatedContractName}' saved successfully."
            : $"Contract '{createdOrUpdatedContractName}' created successfully.";
    }

    public async Task HandleSelectedNodeChangedAsync()
    {
        if (SelectedNode is null || SelectedNode.IsDirectory)
        {
            return;
        }

        if (!File.Exists(SelectedNode.FullPath))
        {
            StatusMessage = $"Selected contract file was not found: {SelectedNode.FullPath}";
            return;
        }

        try
        {
            var request = await TryLoadContractRequestAsync(SelectedNode.FullPath);

            if (request is null)
            {
                StatusMessage = $"Selected file '{SelectedNode.Name}' is not a supported editable contract.";
                return;
            }

            ContractCreator.LoadFromRequest(request, SelectedNode.FullPath);
            StatusMessage = $"Loaded contract '{request.Name}' for editing.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load contract '{SelectedNode.Name}': {ex.Message}";
        }
    }

    private void ClearContractSelection()
    {
        SelectedNode = null;
        ContractCreator.Clear();
        StatusMessage = "Contract selection cleared. Ready to create a new contract.";
    }

    private async Task ReloadTreeAsync()
    {
        RootNode = await _contractsExplorerService.LoadTreeAsync(ContractsDirectoryPath);
    }

    private string ResolveTargetDirectoryPath()
    {
        if (SelectedNode is null)
        {
            return ContractsDirectoryPath;
        }

        return SelectedNode.IsDirectory
            ? SelectedNode.FullPath
            : Path.GetDirectoryName(SelectedNode.FullPath) ?? ContractsDirectoryPath;
    }

    private static async Task<CreateContractRequest?> TryLoadContractRequestAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("ApiToApi", out var apiToApiElement))
        {
            return BuildApiToApiRequest(apiToApiElement);
        }

        if (root.TryGetProperty("PortToApi", out var portToApiElement))
        {
            return BuildPortToApiRequest(portToApiElement);
        }

        if (root.TryGetProperty("FileToApi", out var fileToApiElement))
        {
            return BuildFileToApiRequest(fileToApiElement);
        }

        return null;
    }

    private static CreateContractRequest BuildApiToApiRequest(JsonElement contractElement)
    {
        var request = BuildBaseRequest(contractElement, "ApiToApi");

        if (contractElement.TryGetProperty("Source", out var sourceElement))
        {
            request.SourceEndpoint = GetStringProperty(sourceElement, "Endpoint");
            request.SourceMethod = GetStringProperty(sourceElement, "Method", "GET");
        }

        request.BusinessKeyField = ParseBusinessKeyFields(contractElement);
        return request;
    }

    private static CreateContractRequest BuildPortToApiRequest(JsonElement contractElement)
    {
        var request = BuildBaseRequest(contractElement, "PortToApi");

        if (contractElement.TryGetProperty("Listener", out var listenerElement))
        {
            request.ListenerPath = GetStringProperty(listenerElement, "Path", "/incoming");
            request.ListenerMethod = GetStringProperty(listenerElement, "Method", "POST");
        }

        request.BusinessKeyField = ParseBusinessKeyFields(contractElement);

        return request;
    }

    private static CreateContractRequest BuildFileToApiRequest(JsonElement contractElement)
    {
        var request = BuildBaseRequest(contractElement, "FileToApi");

        if (contractElement.TryGetProperty("Source", out var sourceElement))
        {
            request.FilePath = GetStringProperty(sourceElement, "Directory");
            request.FileType = GetStringProperty(sourceElement, "SearchPattern", "*.csv");
        }

        return request;
    }

    private static CreateContractRequest BuildBaseRequest(JsonElement contractElement, string fallbackType)
    {
        var name = GetStringProperty(contractElement, "Name");
        var contractType = GetStringProperty(contractElement, "ContractType", fallbackType);

        var sinkEndpoint = string.Empty;
        var sinkMethod = "POST";

        if (contractElement.TryGetProperty("Sink", out var sinkElement))
        {
            sinkEndpoint = GetStringProperty(sinkElement, "Endpoint");
            sinkMethod = GetStringProperty(sinkElement, "Method", "POST");
        }

        return new CreateContractRequest
        {
            Name = name,
            ContractType = contractType,
            SinkEndpoint = sinkEndpoint,
            SinkMethod = sinkMethod,
            BusinessKeyField = "id",
            ListenerPath = "/incoming",
            ListenerMethod = "POST",
            FileType = "*.csv",
            Mappings = ParseMappings(contractElement)
        };
    }

    private static string ParseBusinessKeyFields(JsonElement contractElement)
    {
        if (!contractElement.TryGetProperty("BusinessKeyFields", out var businessKeyFieldsElement) ||
            businessKeyFieldsElement.ValueKind != JsonValueKind.Array)
        {
            return "id";
        }

        var keys = new List<string>();

        foreach (var item in businessKeyFieldsElement.EnumerateArray())
        {
            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                keys.Add(value);
            }
        }

        return keys.Count > 0 ? string.Join(", ", keys) : "id";
    }

    private static List<ContractFieldMappingModel> ParseMappings(JsonElement contractElement)
    {
        var mappings = new List<ContractFieldMappingModel>();

        if (!contractElement.TryGetProperty("Mappings", out var mappingsElement) ||
            mappingsElement.ValueKind != JsonValueKind.Array)
        {
            return mappings;
        }

        foreach (var mappingElement in mappingsElement.EnumerateArray())
        {
            var transformation = mappingElement.TryGetProperty("TransformationType", out var transformationTypeElement)
                ? transformationTypeElement.GetString() ?? "Direct"
                : (mappingElement.TryGetProperty("Transformation", out var legacyTransformationElement)
                    ? legacyTransformationElement.GetString() ?? "Direct"
                    : "Direct");

            var sourceFields = new List<string>();
            var targetFields = new List<string>();

            if (mappingElement.TryGetProperty("SourceFieldName", out var sourceFieldNameElement))
            {
                var value = sourceFieldNameElement.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    sourceFields.Add(value);
                }
            }

            if (mappingElement.TryGetProperty("SourceFields", out var sourceFieldsElement) &&
                sourceFieldsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in sourceFieldsElement.EnumerateArray())
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        sourceFields.Add(value);
                    }
                }
            }

            if (mappingElement.TryGetProperty("SinkFieldName", out var sinkFieldNameElement))
            {
                var value = sinkFieldNameElement.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    targetFields.Add(value);
                }
            }

            if (mappingElement.TryGetProperty("SinkFields", out var sinkFieldsElement) &&
                sinkFieldsElement.ValueKind == JsonValueKind.Array)
            {
                var sinkValues = new List<string>();

                foreach (var item in sinkFieldsElement.EnumerateArray())
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        sinkValues.Add(value);
                    }
                }

                if (sinkValues.Count > 0)
                {
                    targetFields.Add(string.Join(";", sinkValues));
                }
            }

            if (mappingElement.TryGetProperty("TargetFields", out var targetFieldsElement) &&
                targetFieldsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in targetFieldsElement.EnumerateArray())
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        targetFields.Add(value);
                    }
                }
            }

            mappings.Add(new ContractFieldMappingModel
            {
                SourceFields = sourceFields,
                TargetFields = targetFields,
                Transformation = transformation,
                Separator = mappingElement.TryGetProperty("Separator", out var separatorElement)
                    ? separatorElement.GetString() ?? " "
                    : " "
            });
        }

        return mappings;
    }

    private static string GetStringProperty(JsonElement element, string propertyName, string fallback = "")
    {
        if (!element.TryGetProperty(propertyName, out var propertyElement))
        {
            return fallback;
        }

        return propertyElement.ValueKind == JsonValueKind.String
            ? propertyElement.GetString() ?? fallback
            : fallback;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}