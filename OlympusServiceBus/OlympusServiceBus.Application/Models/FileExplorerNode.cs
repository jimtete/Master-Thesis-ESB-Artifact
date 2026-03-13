using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OlympusServiceBusApplication.Models;

public class FileExplorerNode : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _fullPath = string.Empty;
    private bool _isDirectory;
    private bool _isExpanded;
    private bool _isSelected;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
        }
    }

    public string FullPath
    {
        get => _fullPath;
        set
        {
            if (_fullPath == value)
            {
                return;
            }

            _fullPath = value;
            OnPropertyChanged();
        }
    }

    public bool IsDirectory
    {
        get => _isDirectory;
        set
        {
            if (_isDirectory == value)
            {
                return;
            }

            _isDirectory = value;
            OnPropertyChanged();
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<FileExplorerNode> Children { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}