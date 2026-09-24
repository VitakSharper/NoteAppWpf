using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NoteApp.Services;
using NoteApp.ViewModels;
using NoteApp.Views;
using static NoteApp.UiTests.Notes;

namespace NoteApp.UiTests;

public class ZoomUiTests
{
    [Fact]
    public void Zoom_moves_in_tenths_between_the_limits_and_is_remembered() => Wpf.Run(() =>
    {
        var (shell, settingsFile) = Shell();

        shell.ZoomEditorCommand.Execute(1);
        shell.ZoomEditorCommand.Execute(1);
        Assert.Equal(1.2, shell.EditorZoom, 3);
        Assert.Equal(1.2, new AppSettingsService(settingsFile).Current.EditorZoom, 3);

        for (var i = 0; i < 30; i++)
            shell.ZoomEditorCommand.Execute(1);
        Assert.Equal(AppSettings.MaxEditorZoom, shell.EditorZoom);

        for (var i = 0; i < 40; i++)
            shell.ZoomEditorCommand.Execute(-1);
        Assert.Equal(AppSettings.MinEditorZoom, shell.EditorZoom);

        shell.ResetEditorZoomCommand.Execute(null);
        Assert.Equal(1.0, shell.EditorZoom);
    });

    [Fact]
    public void The_blocks_scale_with_the_zoom() => Wpf.Run(() =>
    {
        var (shell, _) = Shell();
        var editor = new NoteEditorViewModel(new NoteService(new UnusedNoteRepository()), new UnusedTagRepository(), [], With(Text("zoom me")));
        var window = new Window { DataContext = shell, Content = new NoteEditorView { DataContext = editor }, Left = -10000, Top = -10000, Width = 900, Height = 900, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        Wpf.Pump();
        try
        {
            var list = Wpf.Descendants<ItemsControl>(window).Single(c => c.Name == "BlocksList");
            var scale = Assert.IsType<ScaleTransform>(list.LayoutTransform);
            Assert.Equal(1.0, scale.ScaleX);

            shell.ZoomEditorCommand.Execute(3);
            Wpf.Pump();

            Assert.Equal(1.3, scale.ScaleX, 3);
            Assert.Equal(1.3, scale.ScaleY, 3);
        }
        finally
        {
            window.Close();
        }
    });

    // "Save settings" rebuilt the record from the form's fields and silently reset any
    // setting the form does not show — the zoom being the first of them.
    [Fact]
    public void Saving_the_settings_keeps_the_zoom() => Wpf.Run(() =>
    {
        var (shell, settingsFile) = Shell();
        shell.ZoomEditorCommand.Execute(2);

        shell.SettingsViewModel.SaveCommand.Execute(null);

        Assert.Equal(1.2, new AppSettingsService(settingsFile).Current.EditorZoom, 3);
    });

    private static (MainViewModel Shell, string SettingsFile) Shell()
    {
        var shell = FocusModeUiTests.Shell();
        var file = typeof(MainViewModel).GetField("_settingsService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(shell) is AppSettingsService s ? s.SettingsFilePath : throw new InvalidOperationException();
        return (shell, file);
    }
}
