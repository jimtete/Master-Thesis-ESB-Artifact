using System.Windows;
using OlympusServiceBusApplication.Models.Contracts;
using OlympusServiceBusApplication.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace OlympusServiceBusApplication.Views;

public partial class ScheduleEditorWindow : Window
{
    private readonly ScheduleEditorViewModel _viewModel;

    public ScheduleEditorRequest? ResultSchedule { get; private set; }

    public ScheduleEditorWindow(ScheduleEditorRequest? existingSchedule = null)
    {
        InitializeComponent();

        _viewModel = new ScheduleEditorViewModel();
        _viewModel.LoadFromSchedule(existingSchedule);

        DataContext = _viewModel;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsValid)
        {
            MessageBox.Show(
                _viewModel.ValidationMessage,
                "Invalid Schedule",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        ResultSchedule = _viewModel.BuildSchedule();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}