using System.IO;
using NoteApp.Services;
using NoteApp.ViewModels;

namespace NoteApp.UiTests;

public class ThemeUiTests
{
    // The switch repainted the window at once but only "Save settings" wrote it down, so
    // the next start came back light.
    [Fact]
    public void The_theme_is_remembered_as_soon_as_it_is_switched() => Wpf.Run(() =>
    {
        var file = Path.Combine(Path.GetTempPath(), "NoteApp.UiTests", $"{Guid.NewGuid()}.json");
        var vm = new SettingsViewModel(new AppSettingsService(file), new BackupService("Server=.;Database=none", "none"));
        try
        {
            vm.CloseToTray = true;
            vm.IsDarkMode = true;

            var saved = new AppSettingsService(file).Current;
            Assert.True(saved.IsDarkMode);
            Assert.False(saved.CloseToTray); // the rest of the form still waits for "Save settings"
        }
        finally
        {
            vm.IsDarkMode = false;
        }
    });
}
