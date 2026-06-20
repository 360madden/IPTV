namespace Iptv.App.Services;

public interface IPlaylistDialogService
{
    string? PickPlaylistFile();

    string? PickXmltvFile();

    string? PickOrganizationImportFile();

    string? PickOrganizationExportFile();

    string? PickSmartGroupPresetImportFile();

    string? PickSmartGroupPresetExportFile();

    string? PromptPlaylistUrl();

    void ShowError(string title, string message);
}
