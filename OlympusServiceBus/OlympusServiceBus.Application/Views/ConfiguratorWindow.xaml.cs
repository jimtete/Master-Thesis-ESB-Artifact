using System.Windows;
using System.Windows.Controls;
using OlympusServiceBusApplication.Models;
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
        AddHandler(TreeViewItem.SelectedEvent, new RoutedEventHandler(TreeViewItem_Selected));
    }

    private async void ConfiguratorWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
    }

    private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem treeViewItem)
        {
            return;
        }

        if (treeViewItem.DataContext is FileExplorerNode selectedNode)
        {
            _viewModel.SelectedNode = selectedNode;
        }
    }
}