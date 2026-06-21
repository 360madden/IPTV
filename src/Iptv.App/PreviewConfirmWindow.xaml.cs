using System.Collections.ObjectModel;
using System.Windows;

namespace Iptv.App;

public partial class PreviewConfirmWindow : Window
{
    public PreviewConfirmWindow(string titleText, string messageText, string confirmText, IEnumerable<string> previewLines)
    {
        InitializeComponent();
        DataContext = new PreviewConfirmDialogViewModel(titleText, messageText, confirmText, previewLines);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private sealed record PreviewConfirmDialogViewModel(
        string TitleText,
        string MessageText,
        string ConfirmText,
        IEnumerable<string> Lines)
    {
        public ObservableCollection<string> PreviewLines { get; } = new(Lines);
    }
}
