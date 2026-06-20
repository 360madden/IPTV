using System.Collections.ObjectModel;
using System.Windows;

namespace Iptv.App;

public partial class DuplicatePreviewWindow : Window
{
    public DuplicatePreviewWindow(string titleText, IEnumerable<string> previewLines)
    {
        InitializeComponent();
        DataContext = new DuplicatePreviewDialogViewModel(titleText, previewLines);
    }

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private sealed record DuplicatePreviewDialogViewModel(string TitleText, IEnumerable<string> Lines)
    {
        public ObservableCollection<string> PreviewLines { get; } = new(Lines);
    }
}
