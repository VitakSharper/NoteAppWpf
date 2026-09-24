using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NoteApp.Domain.Models;
using NoteApp.Services;
using NoteApp.Services.Export;
using NoteApp.ViewModels;

namespace NoteApp.Views;

// ⋮ › Version history: the versions a note's saves kept, one shown at a time, and Restore,
// which hands it to the editor (nothing is written until the note is saved).
public partial class VersionHistoryWindow : Window
{
    public sealed record Row(NoteVersion Version)
    {
        public string When => VersionPreview.When(Version.SavedAt);
        public string Details => $"{Version.Title} · {DocAttachment.Size(Version.SizeBytes)}{(Version.IsEncrypted ? " · encrypted" : "")}";
    }

    private readonly NoteEditorViewModel _editor;
    private NoteVersionContent? _shown;
    private int _loading;

    public VersionHistoryWindow(NoteEditorViewModel editor)
    {
        InitializeComponent();
        _editor = editor;
        Loaded += async (_, _) => await LoadAsync();
    }

    internal async Task LoadAsync()
    {
        var versions = await _editor.VersionsAsync();
        Versions.ItemsSource = versions.Select(v => new Row(v)).ToList();
        NoVersions.Visibility = versions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (versions.Count > 0)
            Versions.SelectedIndex = 0;
    }

    private async void OnVersionSelected(object sender, SelectionChangedEventArgs e) => await ShowSelectedAsync();

    // The last selection wins when an earlier one is still loading.
    internal async Task ShowSelectedAsync()
    {
        _shown = null;
        RestoreButton.IsEnabled = false;
        if (Versions.SelectedItem is not Row row)
            return;

        var ticket = ++_loading;
        var result = await _editor.OpenVersionAsync(row.Version.Id);
        if (ticket != _loading)
            return;

        result.Match(
            success: content =>
            {
                _shown = content;
                PreviewTitle.Text = content.Title;
                PreviewLines.ItemsSource = VersionPreview.Lines(content.Blocks);
                PreviewError.Visibility = Visibility.Collapsed;
                RestoreButton.IsEnabled = true;
            },
            failure: error =>
            {
                PreviewTitle.Text = row.Version.Title;
                PreviewLines.ItemsSource = null;
                PreviewError.Text = error.Message;
                PreviewError.Visibility = Visibility.Visible;
            });
    }

    private void OnRestore(object sender, RoutedEventArgs e)
    {
        if (_shown is null)
            return;

        _editor.RestoreVersion(_shown);
        DialogResult = true;
    }

    private void OnClose(object sender, ExecutedRoutedEventArgs e) => Close();
}
