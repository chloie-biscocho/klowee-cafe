using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Klowee.Api.Contracts.Auth;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Klowee.Api.Tests;

/// <summary>
/// Boots the real API (real pipeline, real auth, real controllers) against a
/// throwaway SQLite database.
///
/// Why SQLite and not the EF Core InMemory provider: InMemory is not a
/// relational store — it silently ignores unique indexes, index filters and
/// transactions, which are exactly the behaviours the menu rules depend on
/// (the partial unique index on (menu_version_id, menu_item_id) and the
/// transactional item replacement). SQLite honours all three, so a passing test
/// here means something. It runs in-memory, so it is still fast and disposable.
/// </summary>
public class KloweeApiFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@klowee.test";
    public const string OwnerDisplayName = "Test Owner";
    public const string OwnerPassword = "s3cret-pop-up-cart";
    public const string WrongPassword = "not-the-password";

    private const string TestJwtKey = "klowee-test-signing-key-at-least-32-characters-long";

    public static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly SqliteConnection _connection;
    private readonly Lazy<Task> _initialized;

    /// <summary>The date the app believes it is. Set it before acting in a test.</summary>
    public FakeClock Clock { get; } = new();

    /// <summary>Object storage, in memory. Inspect it to see what an upload stored.</summary>
    public FakeStorageService Storage { get; } = new();

    public KloweeApiFactory()
    {
        // Program.cs reads configuration eagerly, before WebApplicationFactory can
        // inject any, so these have to arrive as environment variables.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Default",
            "Host=unused;Database=unused;Username=unused;Password=unused");
        Environment.SetEnvironmentVariable("Jwt__Key", TestJwtKey);

        // Storage is faked below, but Program.cs checks these are present before
        // the test services are swapped in.
        Environment.SetEnvironmentVariable("Supabase__Url", "https://fake.supabase.test");
        Environment.SetEnvironmentVariable("Supabase__ServiceRoleKey", "test-service-role-key");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "klowee-cafe");
        Environment.SetEnvironmentVariable("Jwt__Audience", "klowee-cafe");

        // An in-memory SQLite database lives exactly as long as a connection to it
        // is open, so this one is held open for the fixture's lifetime.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _initialized = new Lazy<Task>(InitializeDatabaseAsync);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            RemoveDatabaseRegistrations(services);

            services.AddDbContext<KloweeDbContext>(options =>
                options.UseSqlite(_connection).UseSnakeCaseNamingConvention());

            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);

            services.RemoveAll<IStorageService>();
            services.AddSingleton<IStorageService>(Storage);
        });
    }

    /// <summary>Creates the schema and the owner account the tests log in as.</summary>
    public Task EnsureInitializedAsync() => _initialized.Value;

    private async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KloweeDbContext>();

        await db.Database.EnsureCreatedAsync();

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var owner = new User
        {
            Email = OwnerEmail,
            DisplayName = OwnerDisplayName,
            Role = UserRole.Owner
        };
        owner.PasswordHash = hasher.HashPassword(owner, OwnerPassword);

        db.Users.Add(owner);
        await db.SaveChangesAsync();
    }

    /// <summary>Runs arbitrary setup straight against the database.</summary>
    public async Task<T> WithDbAsync<T>(Func<KloweeDbContext, Task<T>> action)
    {
        await EnsureInitializedAsync();

        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<KloweeDbContext>());
    }

    /// <summary>An HttpClient with no Authorization header.</summary>
    public async Task<HttpClient> CreateAnonymousClientAsync()
    {
        await EnsureInitializedAsync();
        return CreateClient();
    }

    /// <summary>An HttpClient that has logged in as the seeded owner.</summary>
    public async Task<HttpClient> CreateOwnerClientAsync()
    {
        var client = await CreateAnonymousClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = OwnerEmail, Password = OwnerPassword });

        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        return client;
    }

    /// <summary>
    /// Drops the app's PostgreSQL wiring so the SQLite registration below is the
    /// only one left. AddDbContext uses TryAdd, so the originals must go first.
    /// </summary>
    private static void RemoveDatabaseRegistrations(IServiceCollection services)
    {
        var doomed = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(KloweeDbContext)
                || descriptor.ServiceType == typeof(DbContextOptions)
                || descriptor.ServiceType == typeof(DbContextOptions<KloweeDbContext>)
                || (descriptor.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") ?? false)
                || (descriptor.ServiceType.FullName?.Contains("Npgsql") ?? false))
            .ToList();

        foreach (var descriptor in doomed)
        {
            services.Remove(descriptor);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
