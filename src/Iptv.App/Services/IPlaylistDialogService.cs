namespace Iptv.App.Services;

public interface IPlaylistDialogService
{
    string? PickPlaylistFile();

    string? PickXmltvFile();

    string? PickOrganizationImportFile();

    string? PickOrganizationExportFile();

    string? PickSmartGroupPresetImportFile();

    string? PickSmartGroupPresetExportFile();

    string? PickSourceProfileImportFile();

    string? PickSourceProfileExportFile();

    string? PickCustomGroupCsvImportFile();

    string? PickCustomGroupCsvExportFile();

    string? PickDiagnosticsExportFile();

    string? PromptPlaylistUrl();

    string? PromptXmltvUrl();

    bool ConfirmDuplicateHide(string title, IReadOnlyList<string> previewLines);

    void ShowError(string title, string message);
}
