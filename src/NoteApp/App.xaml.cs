using System.IO;
using System.Windows;
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
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<App>(optional: true)
            .Build();

        var services = new ServiceCollection();

        var connectionString = configuration.GetConnectionString("NoteDb")
            ?? throw new InvalidOperationException("Connection string 'NoteDb' not found. Run: dotnet user-secrets set \"ConnectionStrings:NoteDb\" \"<your-connection-string>\"");

        services.AddDbContext<NoteDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddSingleton<AppSettingsService>();
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<NoteService>();
        services.AddScoped<SearchService>();

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
}

