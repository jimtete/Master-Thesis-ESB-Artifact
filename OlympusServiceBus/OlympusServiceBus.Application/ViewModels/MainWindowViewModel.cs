using System.ComponentModel;
using System.Runtime.CompilerServices;
using OlympusServiceBusApplication.Models;
using OlympusServiceBusApplication.Services.AppSettingsService;

namespace OlympusServiceBusApplication.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsService _appSettingsService;
    private AppSettings _appSettings = new();
    private string _contractsRootDirectory = string.Empty;

    public string ContractsRootDirectory
    {
        get => _contractsRootDirectory;
        set
        {
            if (_contractsRootDirectory == value)
            {
                return;
            }
            
            _contractsRootDirectory = value;
            OnPropertyChanged();
        }
    }

    public MainWindowViewModel(IAppSettingsService appSettingsService)
    {
        _appSettingsService = appSettingsService;
    }

    public async Task LoadAsync()
    {
        _appSettings = await _appSettingsService.LoadAsync();
        ContractsRootDirectory = _appSettings.ContractRootDirectory;
    }
    
    public async Task SaveAsync()
    {
        _appSettings.ContractRootDirectory = ContractsRootDirectory;
        await _appSettingsService.SaveAsync(_appSettings);
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}