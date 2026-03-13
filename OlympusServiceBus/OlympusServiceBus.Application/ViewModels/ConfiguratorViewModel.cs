using System.ComponentModel;
using System.Runtime.CompilerServices;
using OlympusServiceBusApplication.Models;
using OlympusServiceBusApplication.Services.ContractsService;

namespace OlympusServiceBusApplication.ViewModels;

public class ConfiguratorViewModel : INotifyPropertyChanged
{
    private readonly IContractsWorkspaceService _contractsWorkspaceService;
    private readonly IContractsExplorerService _contractsExplorerService;
    
    private string _contractsDirectoryPath = string.Empty;
    private FileExplorerNode? _rootNode;
    
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

    public ConfiguratorViewModel(
        IContractsWorkspaceService contractsWorkspaceService,
        IContractsExplorerService contractsExplorerService)
    {
        _contractsWorkspaceService = contractsWorkspaceService;
        _contractsExplorerService = contractsExplorerService;
    }

    public async Task LoadAsync()
    {
        ContractsDirectoryPath = await _contractsWorkspaceService.EnsureContractsDirectoryAsync();
        RootNode = await _contractsExplorerService.LoadTreeAsync(ContractsDirectoryPath);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}