using Klowee.ContentSync;

namespace ContentSync.Tests;

public class SyncSettingsTests
{
    private const string Pooler = "aws-0-ap-southeast-1.pooler.supabase.com";

    private static Dictionary<string, string?> Env(string sourceRef = "devref", string destRef = "prodref", string? sourceUrlRef = null) => new()
    {
        ["SYNC_SOURCE_DB"] = $"Host={Pooler};Port=5432;Database=postgres;Username=postgres.{sourceRef};Password=a",
        ["SYNC_SOURCE_SUPABASE_URL"] = $"https://{sourceUrlRef ?? sourceRef}.supabase.co",
        ["SYNC_SOURCE_SERVICE_KEY"] = "source-key",
        ["SYNC_DEST_DB"] = $"Host={Pooler};Port=5432;Database=postgres;Username=postgres.{destRef};Password=b",
        ["SYNC_DEST_SUPABASE_URL"] = $"https://{destRef}.supabase.co",
        ["SYNC_DEST_SERVICE_KEY"] = "dest-key"
    };

    [Fact]
    public void TwoProjectsOnTheSamePoolerHost_AreToldApartByTheirRefs()
    {
        var env = Env();
        var (source, destination) = SyncSettings.Load(name => env[name]);

        Assert.Equal("devref", source.ProjectRef);
        Assert.Equal("prodref", destination.ProjectRef);
    }

    [Fact]
    public void AMissingVariable_IsNamed()
    {
        var env = Env();
        env["SYNC_DEST_SERVICE_KEY"] = null;

        var error = Assert.Throws<SettingsException>(() => SyncSettings.Load(name => env[name]));
        Assert.Contains("SYNC_DEST_SERVICE_KEY", error.Message);
    }

    [Fact]
    public void TheSameProjectOnBothSides_IsRefused()
    {
        var env = Env(sourceRef: "prodref", destRef: "prodref");

        Assert.Throws<SettingsException>(() => SyncSettings.Load(name => env[name]));
    }

    [Fact]
    public void ADatabaseAndABucketFromDifferentProjects_AreRefused()
    {
        // The dev database paired with the prod bucket would rewrite URLs wrongly.
        var env = Env(sourceUrlRef: "prodref");

        var error = Assert.Throws<SettingsException>(() => SyncSettings.Load(name => env[name]));
        Assert.DoesNotContain("Password", error.Message);
    }

    [Fact]
    public void AUriConnectionString_IsRefusedWithoutEchoingIt()
    {
        var env = Env();
        env["SYNC_SOURCE_DB"] = "postgresql://postgres.devref:secret-pass@host:5432/postgres";

        var error = Assert.Throws<SettingsException>(() => SyncSettings.Load(name => env[name]));
        Assert.DoesNotContain("secret-pass", error.Message);
    }

    [Theory]
    [InlineData(Pooler, "postgres.abcd", "abcd")]
    [InlineData("db.abcd.supabase.co", "postgres", "abcd")]
    [InlineData("localhost", "postgres", null)]
    public void TheProjectRef_ComesFromThePoolerUsernameOrTheDirectHost(string host, string user, string? expected) =>
        Assert.Equal(expected, SyncSettings.RefFromDatabase(host, user));
}
