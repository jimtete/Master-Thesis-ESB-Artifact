using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OlympusServiceBusApplication.Services.AppSettingsService;
using OlympusServiceBusApplication.ViewModels;

namespace OlympusServiceBusApplication;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        
        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.LoadAsync();
        }
        catch (Exception _)
        {
            // ignored
        }
    }
}