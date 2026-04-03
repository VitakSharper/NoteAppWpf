using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using NoteApp.Services;

namespace NoteApp.ViewModels;

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

    public IReadOnlyList<StartupPage> StartupPages { get; } = Enum.GetValues<StartupPage>();
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
            BackupFolderPath));
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
    }

    public static void ApplyTheme(bool isDark)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);

        if (isDark)
        {
            // Lighter Indigo (300) for better contrast on dark backgrounds
            var lightIndigo = Color.FromRgb(0x79, 0x86, 0xCB);
            theme.SetPrimaryColor(lightIndigo);
            theme.SetSecondaryColor(Color.FromRgb(0x4D, 0xB6, 0xAC)); // Teal 300
        }
        else
        {
            theme.SetPrimaryColor(Color.FromRgb(0x3F, 0x51, 0xB5)); // Indigo 500
            theme.SetSecondaryColor(Color.FromRgb(0x00, 0x96, 0x88)); // Teal 500
        }

        paletteHelper.SetTheme(theme);
    }
}
