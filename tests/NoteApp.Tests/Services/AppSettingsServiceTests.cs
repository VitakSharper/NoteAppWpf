using System.IO;
using NoteApp.Services;
using NoteApp.ViewModels;

namespace NoteApp.Tests.Services;

// Settings are read from disk at startup, so an older file has to keep working. Each
// test uses its own temporary path and never touches the real %LocalAppData% one.
public class AppSettingsServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), "NoteApp.Tests", $"settings-{Guid.NewGuid()}.json");

    [Fact]
    public void A_missing_file_yields_the_defaults()
    {
        var service = new AppSettingsService(_path);

        Assert.Equal(5, service.Current.LockEncryptedNotesAfterMinutes);
        Assert.True(service.Current.ConfirmNoteDeletion);
    }

    // The file written before the lock setting existed: taking "no value" for "never"
    // would silently disable the feature for everyone who already ran the app.
    [Fact]
    public void A_file_without_the_lock_setting_takes_the_default_not_never()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, """
            {
              "ConfirmNoteDeletion": false,
              "ConfirmTagDeletion": true,
              "LaunchPage": "Tags",
              "IsDarkMode": true,
              "BackupFolderPath": "C:\\Backups"
            }
            """);

        var settings = new AppSettingsService(_path).Current;

        Assert.Equal(5, settings.LockEncryptedNotesAfterMinutes);
        // and the rest of the file is still honoured
        Assert.False(settings.ConfirmNoteDeletion);
        Assert.True(settings.IsDarkMode);
        Assert.Equal(StartupPage.Tags, settings.LaunchPage);
        Assert.Equal("C:\\Backups", settings.BackupFolderPath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(30)]
    public void The_lock_delay_survives_a_save_and_reload(int minutes)
    {
        var service = new AppSettingsService(_path);

        service.Save(service.Current with { LockEncryptedNotesAfterMinutes = minutes });

        Assert.Equal(minutes, new AppSettingsService(_path).Current.LockEncryptedNotesAfterMinutes);
    }

    [Fact]
    public void Unreadable_content_falls_back_to_the_defaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, "{ not json");

        Assert.Equal(AppSettings.Default, new AppSettingsService(_path).Current);
    }

    [Fact]
    public void The_delay_options_read_as_words()
    {
        Assert.Equal("Never", LockTimeoutOption.For(0).ToString());
        Assert.Equal("1 minute", LockTimeoutOption.For(1).ToString());
        Assert.Equal("5 minutes", LockTimeoutOption.For(5).ToString());
    }

    // A hand-edited value that is not one of the offered options.
    [Fact]
    public void An_unlisted_delay_falls_back_to_the_default_option() =>
        Assert.Equal(5, LockTimeoutOption.For(7).Minutes);

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
