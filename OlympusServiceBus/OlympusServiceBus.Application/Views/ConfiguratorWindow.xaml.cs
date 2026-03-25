using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OlympusServiceBusApplication.Models;
using OlympusServiceBusApplication.ViewModels;
using MessageBox = System.Windows.MessageBox;

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

    private void ContractsTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var treeViewItem = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);

        if (treeViewItem is null)
        {
            return;
        }

        treeViewItem.Focus();
        treeViewItem.IsSelected = true;
        e.Handled = false;
    }

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearContractSelectionCommand.Execute(null);
        ClearTreeViewSelection(ContractsTreeView);
    }

    private async void DeleteSelectedNode_Click(object sender, RoutedEventArgs e)
    {
        var selectedNode = _viewModel.SelectedNode;

        if (selectedNode is null)
        {
            return;
        }

        if (selectedNode.IsDirectory)
        {
            MessageBox.Show(
                "Folder deletion is not supported yet.",
                "Delete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{selectedNode.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (File.Exists(selectedNode.FullPath))
        {
            File.Delete(selectedNode.FullPath);
        }

        if (string.Equals(
                _viewModel.ContractCreator.SelectedContractFilePath,
                selectedNode.FullPath,
                StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.ClearContractSelectionCommand.Execute(null);
            ClearTreeViewSelection(ContractsTreeView);
        }

        await _viewModel.LoadAsync();
        _viewModel.StatusMessage = $"Contract '{selectedNode.Name}' deleted successfully.";
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

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}