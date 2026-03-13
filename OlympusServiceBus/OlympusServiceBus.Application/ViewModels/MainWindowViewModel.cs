using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OlympusServiceBusApplication.Commands;
using OlympusServiceBusApplication.Models;
using OlympusServiceBusApplication.Services.AppSettingsService;
using OlympusServiceBusApplication.Services.FolderPickerService;

namespace OlympusServiceBusApplication.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IFolderPickerService _folderPickerService;
    
    private AppSettings _appSettings = new();
    private string _contractsRootDirectory = string.Empty;
    private bool _isSetupComplete;

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
            UpdateIsSetupComplete();
        }
    }
    
    public bool IsSetupComplete
    {
        get => _isSetupComplete;
        set
        {
            if (_isSetupComplete == value)
            {
                return;
            }
            
            _isSetupComplete = value;
            OnPropertyChanged();
        }
    }
    
    public ICommand BrowseContractsRootDirectoryCommand { get; }

    public MainWindowViewModel(
        IAppSettingsService appSettingsService,
        IFolderPickerService folderPickerService)
    {
        _appSettingsService = appSettingsService;
        _folderPickerService = folderPickerService;

        BrowseContractsRootDirectoryCommand = new AsyncRelayCommand(BrowseContractsRootDirectoryAsync);
    }

    public async Task LoadAsync()
    {
        _appSettings = await _appSettingsService.LoadAsync();
        ContractsRootDirectory = _appSettings.ContractRootDirectory;
        UpdateIsSetupComplete();
    }
    
    public async Task SaveAsync()
    {
        _appSettings.ContractRootDirectory = ContractsRootDirectory;
        await _appSettingsService.SaveAsync(_appSettings);
        UpdateIsSetupComplete();
    }

    private async Task BrowseContractsRootDirectoryAsync()
    {
        var selectedFolder = _folderPickerService.PickFolder(ContractsRootDirectory);

        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            return;
        }
        
        ContractsRootDirectory = selectedFolder;
        await SaveAsync();
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    #region PRIVATE HELPERS
    
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void UpdateIsSetupComplete()
    {
        IsSetupComplete = !string.IsNullOrWhiteSpace(ContractsRootDirectory);
    }
    
    #endregion
}