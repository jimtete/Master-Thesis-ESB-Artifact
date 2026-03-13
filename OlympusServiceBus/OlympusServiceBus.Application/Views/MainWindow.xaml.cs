using System.Windows;
using OlympusServiceBusApplication.ViewModels;

namespace OlympusServiceBusApplication.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ConfiguratorWindow _configuratorWindow;
    
    public MainWindow(MainWindowViewModel viewModel, ConfiguratorWindow configuratorWindow)
    {
        InitializeComponent();
        
        _configuratorWindow = configuratorWindow;
        
        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.LoadAsync();

            if (!string.IsNullOrWhiteSpace(_viewModel.ContractsRootDirectory))
            {
                _configuratorWindow.Show();
                Close();
            }
        }
        catch (Exception _)
        {
            // ignored
        }
    }
}