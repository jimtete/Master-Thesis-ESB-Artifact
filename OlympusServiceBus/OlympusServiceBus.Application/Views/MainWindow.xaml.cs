using System.ComponentModel;
using System.Windows;
using OlympusServiceBusApplication.ViewModels;

namespace OlympusServiceBusApplication.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ConfiguratorWindow _configuratorWindow;
    private bool _hasRedirected;

    public MainWindow(
        MainWindowViewModel viewModel,
        ConfiguratorWindow configuratorWindow)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _configuratorWindow = configuratorWindow;

        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
        RedirectToConfiguratorIfReady();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsSetupComplete))
        {
            RedirectToConfiguratorIfReady();
        }
    }

    private void RedirectToConfiguratorIfReady()
    {
        if (_hasRedirected || !_viewModel.IsSetupComplete)
        {
            return;
        }

        _hasRedirected = true;
        _configuratorWindow.Show();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        base.OnClosed(e);
    }
}