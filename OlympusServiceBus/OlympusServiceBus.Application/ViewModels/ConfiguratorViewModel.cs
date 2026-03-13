using System.ComponentModel;
using System.Runtime.CompilerServices;
using OlympusServiceBusApplication.Services.ContractsWorkspaceService;

namespace OlympusServiceBusApplication.ViewModels;

public class ConfiguratorViewModel : INotifyPropertyChanged
{
    private readonly IContractsWorkspaceService _contractsWorkspaceService;
    
    private string _contractsDirectoryPath = string.Empty;
    
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

    public ConfiguratorViewModel(IContractsWorkspaceService contractsWorkspaceService)
    {
        _contractsWorkspaceService = contractsWorkspaceService;
    }

    public async Task LoadAsync()
    {
        ContractsDirectoryPath = await _contractsWorkspaceService.EnsureContractsDirectoryAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}