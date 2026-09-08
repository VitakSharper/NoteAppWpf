using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NoteApp.Data;
using NoteApp.Data.Repositories;
using NoteApp.Services;
using NoteApp.ViewModels;

namespace NoteApp;

public partial class App : Application
{
    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NoteApp",
        "crash.log");

    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // BaseDirectory, not the current directory: the latter depends on how the
        // exe was launched (shortcut, terminal, double-click) and appsettings.json
        // sits next to the binary.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<App>(optional: true)
            .Build();

        var services = new ServiceCollection();

        var connectionString = configuration.GetConnectionString("NoteDb")
            ?? throw new InvalidOperationException("Connection string 'NoteDb' not found. Run: dotnet user-secrets set \"ConnectionStrings:NoteDb\" \"<your-connection-string>\"");

        // A factory rather than a scoped context: everything below is resolved
        // from the root provider, so a scoped DbContext would live as long as the
        // app and fail as soon as two async operations overlapped.
        services.AddDbContextFactory<NoteDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddSingleton<AppSettingsService>();
        services.AddSingleton<INoteRepository, NoteRepository>();
        services.AddSingleton<ITagRepository, TagRepository>();
        services.AddSingleton<NoteService>();

        var sqlBuilder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        services.AddSingleton(new BackupService(connectionString, sqlBuilder.InitialCatalog));

        services.AddTransient<NoteListViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<TagManagerViewModel>();
        services.AddTransient<MainViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        // Apply saved theme before showing the window
        var settingsService = _serviceProvider.GetRequiredService<AppSettingsService>();
        SettingsViewModel.ApplyTheme(settingsService.Current.IsDarkMode);

        var mainWindow = new MainWindow
        {
            DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nDetails were written to:\n{CrashLogPath}",
            "NoteApp", MessageBoxButton.OK, MessageBoxImage.Error);

        e.Handled = true;

        // Before the window exists there is nothing to keep alive (e.g. missing
        // connection string); afterwards, staying up beats losing unsaved notes.
        if (MainWindow is null)
            Shutdown(1);
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            LogCrash(exception);
    }

    private static void LogCrash(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never be the reason the app dies.
        }
    }
}
