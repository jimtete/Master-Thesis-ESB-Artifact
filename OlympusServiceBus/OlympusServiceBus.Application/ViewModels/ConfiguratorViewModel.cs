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

        if (!document.RootElement.TryGetProperty("ApiToApi", out var apiToApiElement))
        {
            return null;
        }

        var name = apiToApiElement.TryGetProperty("Name", out var nameElement)
            ? nameElement.GetString() ?? string.Empty
            : string.Empty;

        var contractType = apiToApiElement.TryGetProperty("ContractType", out var contractTypeElement)
            ? contractTypeElement.GetString() ?? "ApiToApi"
            : "ApiToApi";

        var sourceEndpoint = string.Empty;
        var sourceMethod = "GET";

        if (apiToApiElement.TryGetProperty("Source", out var sourceElement))
        {
            if (sourceElement.TryGetProperty("Endpoint", out var endpointElement))
            {
                sourceEndpoint = endpointElement.GetString() ?? string.Empty;
            }

            if (sourceElement.TryGetProperty("Method", out var methodElement))
            {
                sourceMethod = methodElement.GetString() ?? "GET";
            }
        }

        var sinkEndpoint = string.Empty;
        var sinkMethod = "POST";

        if (apiToApiElement.TryGetProperty("Sink", out var sinkElement))
        {
            if (sinkElement.TryGetProperty("Endpoint", out var endpointElement))
            {
                sinkEndpoint = endpointElement.GetString() ?? string.Empty;
            }

            if (sinkElement.TryGetProperty("Method", out var methodElement))
            {
                sinkMethod = methodElement.GetString() ?? "POST";
            }
        }

        var businessKeyField = "id";

        if (apiToApiElement.TryGetProperty("BusinessKeyFields", out var businessKeyFieldsElement) &&
            businessKeyFieldsElement.ValueKind == JsonValueKind.Array &&
            businessKeyFieldsElement.GetArrayLength() > 0)
        {
            businessKeyField = businessKeyFieldsElement[0].GetString() ?? "id";
        }

        var mappings = new List<ContractFieldMappingModel>();

        if (apiToApiElement.TryGetProperty("Mappings", out var mappingsElement) &&
            mappingsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var mappingElement in mappingsElement.EnumerateArray())
            {
                var sourceFields = new List<string>();
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

                var targetFields = new List<string>();
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
                    Transformation = mappingElement.TryGetProperty("Transformation", out var transformationElement)
                        ? transformationElement.GetString() ?? "Direct"
                        : "Direct",
                    Separator = mappingElement.TryGetProperty("Separator", out var separatorElement)
                        ? separatorElement.GetString() ?? " "
                        : " "
                });
            }
        }

        return new CreateContractRequest
        {
            Name = name,
            ContractType = contractType,
            SourceEndpoint = sourceEndpoint,
            SourceMethod = sourceMethod,
            SinkEndpoint = sinkEndpoint,
            SinkMethod = sinkMethod,
            BusinessKeyField = businessKeyField,
            Mappings = mappings
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}