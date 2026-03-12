namespace OlympusServiceBusApplication.Services.FolderPickerService;

public interface IFolderPickerService
{
    string? PickFolder(string? initialDirectory = null);
}