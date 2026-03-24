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

    private async void TreeViewItem_Selected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem treeViewItem)
        {
            return;
        }

        if (treeViewItem.DataContext is FileExplorerNode selectedNode)
        {
            _viewModel.SelectedNode = selectedNode;
            await _viewModel.HandleSelectedNodeChangedAsync();
        }
    }

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearContractSelectionCommand.Execute(null);
        ClearTreeViewSelection(ContractsTreeView);
    }

    private static void ClearTreeViewSelection(ItemsControl parent)
    {
        foreach (var item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem)
            {
                treeViewItem.IsSelected = false;
                ClearTreeViewSelection(treeViewItem);
            }
        }
    }
}