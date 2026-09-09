using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using NoteApp.Services;

namespace NoteApp.ViewModels;

// The lock delay is picked from a list, so the label lives with the value.
public sealed record LockTimeoutOption(int Minutes)
{
    public override string ToString() => Minutes switch
    {
        0 => "Never",
        1 => "1 minute",
        _ => $"{Minutes} minutes"
    };

    public static readonly IReadOnlyList<LockTimeoutOption> All =
        [new(0), new(1), new(2), new(5), new(10), new(15), new(30)];

    public static LockTimeoutOption For(int minutes) =>
        All.FirstOrDefault(o => o.Minutes == minutes)
        ?? All.Single(o => o.Minutes == AppSettings.Default.LockEncryptedNotesAfterMinutes);
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsService _settingsService;
    private readonly BackupService _backupService;

    [ObservableProperty] private bool _confirmNoteDeletion;
    [ObservableProperty] private bool _confirmTagDeletion;
    [ObservableProperty] private StartupPage _startupPage;
    [ObservableProperty] private bool _isDarkMode;
    [ObservableProperty] private string _backupPassword = "";
    [ObservableProperty] private string _backupFolderPath = AppSettings.DefaultBackupFolderPath;
    [ObservableProperty] private bool _isBackingUp;
    [ObservableProperty] private LockTimeoutOption _lockTimeout = LockTimeoutOption.For(AppSettings.Default.LockEncryptedNotesAfterMinutes);

    public IReadOnlyList<StartupPage> StartupPages { get; } = Enum.GetValues<StartupPage>();
    public IReadOnlyList<LockTimeoutOption> LockTimeouts { get; } = LockTimeoutOption.All;
    public string SettingsFilePath => _settingsService.SettingsFilePath;

    public event Action<string>? ShowMessage;
    public event Action? CloseRequested;

    public bool CanBackup => !IsBackingUp && !string.IsNullOrWhiteSpace(BackupPassword);

    public SettingsViewModel(AppSettingsService settingsService, BackupService backupService)
    {
        _settingsService = settingsService;
        _backupService = backupService;
        LoadFrom(_settingsService.Current);
    }

    partial void OnIsBackingUpChanged(bool value) => OnPropertyChanged(nameof(CanBackup));
    partial void OnBackupPasswordChanged(string value) => OnPropertyChanged(nameof(CanBackup));

    partial void OnIsDarkModeChanged(bool value) =>
        ApplyTheme(value);

    private void PersistSettings()
    {
        _settingsService.Save(new AppSettings(
            ConfirmNoteDeletion,
            ConfirmTagDeletion,
            StartupPage,
            IsDarkMode,
            BackupPassword,
            BackupFolderPath,
            LockTimeout.Minutes));
    }

    [RelayCommand]
    private void Save()
    {
        PersistSettings();
        ShowMessage?.Invoke("Settings saved.");
    }

    [RelayCommand]
    private async Task BackupAsync()
    {
        PersistSettings();

        IsBackingUp = true;
        try
        {
            var result = await _backupService.ExportAsync(BackupPassword, BackupFolderPath);
            result.Match(
                success: path => ShowMessage?.Invoke($"Backup saved to {path}"),
                failure: error => ShowMessage?.Invoke($"Backup failed: {error.Message}"));
        }
        finally
        {
            IsBackingUp = false;
        }
    }

    [RelayCommand]
    private void BrowseBackupFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select backup folder" };

        if (Directory.Exists(BackupFolderPath))
            dialog.InitialDirectory = BackupFolderPath;

        if (dialog.ShowDialog() == true)
            BackupFolderPath = dialog.FolderName;
    }

    [RelayCommand]
    private void OpenBackupFolder()
    {
        // Walk up to the nearest existing ancestor so Explorer always opens something
        var path = BackupFolderPath;
        while (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            path = Path.GetDirectoryName(path);

        if (string.IsNullOrEmpty(path))
            path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    [RelayCommand]
    private void ResetDefaults()
    {
        LoadFrom(AppSettings.Default);
        Save();
    }

    private void LoadFrom(AppSettings settings)
    {
        ConfirmNoteDeletion = settings.ConfirmNoteDeletion;
        ConfirmTagDeletion = settings.ConfirmTagDeletion;
        StartupPage = settings.LaunchPage;
        IsDarkMode = settings.IsDarkMode;
        BackupPassword = settings.BackupPassword;
        BackupFolderPath = settings.BackupFolderPath;
        LockTimeout = LockTimeoutOption.For(settings.LockEncryptedNotesAfterMinutes);
    }

    public static void ApplyTheme(bool isDark)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);

        // Modern Violet accent
        var violet = Color.FromRgb(0x6C, 0x79, 0xFF);
        var violetLight = Color.FromRgb(0x9A, 0xA3, 0xFF); // lighter for dark-mode contrast
        theme.SetPrimaryColor(isDark ? violetLight : violet);
        theme.SetSecondaryColor(isDark ? violetLight : violet);

        paletteHelper.SetTheme(theme);
    }
}
