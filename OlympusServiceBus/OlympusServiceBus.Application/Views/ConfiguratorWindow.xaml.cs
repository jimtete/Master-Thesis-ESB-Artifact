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
        System.Windows.DataObject.AddPastingHandler(ContractNameTextBox, ContractNameTextBox_Pasting);
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

    private void ConfigureScheduleButton_Click(object sender, RoutedEventArgs e)
    {
        var scheduleWindow = new ScheduleEditorWindow(_viewModel.ContractCreator.Schedule)
        {
            Owner = this
        };

        var result = scheduleWindow.ShowDialog();

        if (result != true || scheduleWindow.ResultSchedule is null)
        {
            return;
        }

        _viewModel.ContractCreator.Schedule = scheduleWindow.ResultSchedule;
        _viewModel.StatusMessage = "Scheduling configuration updated.";
    }

    private async void ExecuteContractButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not FrameworkElement frameworkElement ||
            frameworkElement.DataContext is not FileExplorerNode node)
        {
            return;
        }

        await _viewModel.ExecuteManualContractAsync(node);
    }

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearContractSelectionCommand.Execute(null);
        ClearTreeViewSelection(ContractsTreeView);
    }

    private void ContractsTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var selectedNode = _viewModel.SelectedNode;
        var isContract = selectedNode is { IsDirectory: false };

        EnableContractMenuItem.IsEnabled = isContract && selectedNode is { IsContractEnabled: false };
        DisableContractMenuItem.IsEnabled = isContract && selectedNode is { IsContractEnabled: true };
    }

    private async void EnableSelectedContract_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SetContractEnabledAsync(_viewModel.SelectedNode, true);
    }

    private async void DisableSelectedContract_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SetContractEnabledAsync(_viewModel.SelectedNode, false);
    }

    private void ContractNameTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = ContainsWhitespace(e.Text);
    }

    private void ContractNameTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText, true))
        {
            return;
        }

        if (e.SourceDataObject.GetData(System.Windows.DataFormats.UnicodeText) is not string pastedText)
        {
            return;
        }

        if (ContainsWhitespace(pastedText))
        {
            e.CancelCommand();
        }
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

    private static bool ContainsWhitespace(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                return true;
            }
        }

        return false;
    }
}
