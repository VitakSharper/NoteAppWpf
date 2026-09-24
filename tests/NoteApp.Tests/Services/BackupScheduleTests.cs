using System.IO;
using NoteApp.Services;

namespace NoteApp.Tests.Services;

// The automatic backup deletes folders, so which ones it considers its own is pinned here.
public class BackupScheduleTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "NoteApp.Tests", $"backups-{Guid.NewGuid()}");

    [Fact]
    public void Folder_names_round_trip_their_timestamp()
    {
        var name = BackupSchedule.FolderName(Now);

        Assert.Equal("NoteApp_2026-09-24_120000", name);
        Assert.Equal(Now, BackupSchedule.TimestampOf(name).GetValueOrDefault(DateTime.MinValue));
    }

    [Theory]
    [InlineData("Photos")]
    [InlineData("NoteApp_")]
    [InlineData("NoteApp_yesterday")]
    [InlineData("noteapp_2026-09-24_120000")]
    [InlineData("NoteApp_2026-09-24_120000_copy")]
    public void Other_folders_are_not_backups(string name) =>
        Assert.True(BackupSchedule.TimestampOf(name).IsNone);

    [Fact]
    public void Off_is_never_due() =>
        Assert.False(BackupSchedule.IsDue(AutoBackupInterval.Off, [], Now));

    [Fact]
    public void The_first_backup_is_due_at_once() =>
        Assert.True(BackupSchedule.IsDue(AutoBackupInterval.Weekly, ["Photos"], Now));

    [Theory]
    [InlineData(AutoBackupInterval.Daily, 23, false)]
    [InlineData(AutoBackupInterval.Daily, 24, true)]
    [InlineData(AutoBackupInterval.Weekly, 24 * 6, false)]
    [InlineData(AutoBackupInterval.Weekly, 24 * 7, true)]
    public void Due_once_the_newest_backup_is_a_period_old(AutoBackupInterval interval, int hoursAgo, bool due)
    {
        string[] existing = [BackupSchedule.FolderName(Now.AddDays(-30)), BackupSchedule.FolderName(Now.AddHours(-hoursAgo))];

        Assert.Equal(due, BackupSchedule.IsDue(interval, existing, Now));
    }

    [Fact]
    public void Deletes_the_oldest_beyond_the_kept_number()
    {
        var names = Enumerable.Range(1, 5).Select(d => BackupSchedule.FolderName(Now.AddDays(-d))).ToList();

        var deleted = BackupSchedule.ToDelete([.. names, "Photos"], keep: 3);

        Assert.Equal([names[3], names[4]], deleted);
    }

    [Fact]
    public void Keeping_zero_keeps_everything() =>
        Assert.Empty(BackupSchedule.ToDelete([BackupSchedule.FolderName(Now), BackupSchedule.FolderName(Now.AddDays(-1))], keep: 0));

    // A backup folder the user put something else into is not ours to delete any more.
    [Fact]
    public void Prune_only_removes_folders_holding_nothing_but_their_archive()
    {
        var oldest = Backup(Now.AddDays(-3));
        var tampered = Backup(Now.AddDays(-2));
        File.WriteAllText(Path.Combine(_folder, tampered, "notes.txt"), "mine");
        var newest = Backup(Now.AddDays(-1));
        Directory.CreateDirectory(Path.Combine(_folder, "Photos"));

        var deleted = BackupService.Prune(_folder, keep: 1);

        Assert.Equal(1, deleted);
        Assert.False(Directory.Exists(Path.Combine(_folder, oldest)));
        Assert.True(Directory.Exists(Path.Combine(_folder, tampered)));
        Assert.True(Directory.Exists(Path.Combine(_folder, newest)));
        Assert.True(Directory.Exists(Path.Combine(_folder, "Photos")));
    }

    private string Backup(DateTime timestamp)
    {
        var name = BackupSchedule.FolderName(timestamp);
        Directory.CreateDirectory(Path.Combine(_folder, name));
        File.WriteAllText(Path.Combine(_folder, name, $"{name}.zip"), "zip");
        return name;
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }
}
