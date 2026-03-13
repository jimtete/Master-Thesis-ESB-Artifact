using System.Windows;
using OlympusServiceBusApplication.ViewModels;

namespace OlympusServiceBusApplication.Views;

public partial class ConfiguratorWindow : Window
{
    private readonly ConfiguratorViewModel _viewModel;
    
    public ConfiguratorWindow(ConfiguratorViewModel viewModel)
    {
        InitializeComponent();
        
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += ConfiguratorWindow_Loaded;
    }

    private async void ConfiguratorWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
    }
}