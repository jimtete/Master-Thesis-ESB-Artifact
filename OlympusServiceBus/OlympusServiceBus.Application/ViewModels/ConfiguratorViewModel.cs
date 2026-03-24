using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OlympusServiceBusApplication.Commands;
using OlympusServiceBusApplication.Models;
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
    private string _newContractName = string.Empty;
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

    public string NewContractName
    {
        get => _newContractName;
        set
        {
            if (_newContractName == value)
            {
                return;
            }

            _newContractName = value;
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

        await _contractsExplorerService.CreateContractFileAsync(parentPath, request);
        var createdContractName = request.Name;

        await ReloadTreeAsync();

        StatusMessage = $"Contract '{createdContractName}' created successfully.";
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}