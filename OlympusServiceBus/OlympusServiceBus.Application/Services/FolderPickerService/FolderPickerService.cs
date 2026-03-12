using System.IO;

namespace OlympusServiceBusApplication.Services.FolderPickerService;

public class FolderPickerService : IFolderPickerService
{
    public string? PickFolder(string? initialDirectory = null)
    {
        using var dialog = new FolderBrowserDialog();

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        var result = dialog.ShowDialog();
        
        return result == DialogResult.OK ? dialog.SelectedPath : null;
    }
}