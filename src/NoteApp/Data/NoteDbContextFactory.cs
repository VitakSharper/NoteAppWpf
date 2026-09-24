using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace NoteApp.Data;

public sealed class NoteDbContextFactory : IDesignTimeDbContextFactory<NoteDbContext>
{
    public NoteDbContext CreateDbContext(string[] args)
    {
        // Same override as the app (App.EnvironmentPrefix): with NOTEAPP_ConnectionStrings__NoteDb
        // set, `dotnet ef database update` migrates the scratch database, not the real one.
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<NoteDbContextFactory>()
            .AddEnvironmentVariables(App.EnvironmentPrefix)
            .Build();

        var connectionString = configuration.GetConnectionString("NoteDb")
            ?? throw new InvalidOperationException("Connection string 'NoteDb' not found in user secrets.");

        var options = new DbContextOptionsBuilder<NoteDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new NoteDbContext(options);
    }
}
