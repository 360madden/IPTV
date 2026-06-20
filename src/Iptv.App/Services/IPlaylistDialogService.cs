namespace Iptv.App.Services;

public interface IPlaylistDialogService
{
    string? PickPlaylistFile();

    string? PickXmltvFile();

    string? PromptPlaylistUrl();

    void ShowError(string title, string message);
}
