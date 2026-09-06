using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Klowee.Api.Data;

/// <summary>
/// Design-time factory used by the EF Core tools (`dotnet ef migrations add`,
/// `dotnet ef database update`). It resolves the connection string from the same
/// sources as the running app (appsettings, user-secrets, environment) and falls
/// back to a local placeholder so `migrations add` works offline, before any real
/// database exists.
/// </summary>
public class KloweeDbContextFactory : IDesignTimeDbContextFactory<KloweeDbContext>
{
    public KloweeDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(typeof(KloweeDbContextFactory).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Placeholder is only used for offline model operations (e.g. migrations add);
        // it is never connected to.
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=klowee_design;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<KloweeDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new KloweeDbContext(options);
    }
}
