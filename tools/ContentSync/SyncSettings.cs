using Npgsql;

namespace Klowee.ContentSync;

/// <summary>One side of a sync: a database and the project whose bucket goes with it.</summary>
public sealed record Endpoint(string Name, string ConnectionString, StorageUrls Storage, string ServiceKey, string ProjectRef, string DbHost)
{
    /// <summary>What may be printed about this side: never the connection string or key.</summary>
    public string Describe() => $"{Name}: project {ProjectRef}, database host {DbHost}";
}

/// <summary>
/// Reads both sides from the environment and refuses anything that could
/// point the tool at the wrong place. Nothing secret is ever echoed: only
/// project refs and host names.
/// </summary>
public static class SyncSettings
{
    public static readonly string[] Variables =
    [
        "SYNC_SOURCE_DB", "SYNC_SOURCE_SUPABASE_URL", "SYNC_SOURCE_SERVICE_KEY",
        "SYNC_DEST_DB", "SYNC_DEST_SUPABASE_URL", "SYNC_DEST_SERVICE_KEY"
    ];

    public static (Endpoint Source, Endpoint Destination) Load(Func<string, string?> getVariable)
    {
        var missing = Variables.Where(name => string.IsNullOrWhiteSpace(getVariable(name))).ToList();
        if (missing.Count > 0)
        {
            throw new SettingsException($"Missing environment variable(s): {string.Join(", ", missing)}.");
        }

        var source = Side("source", getVariable("SYNC_SOURCE_DB")!, getVariable("SYNC_SOURCE_SUPABASE_URL")!, getVariable("SYNC_SOURCE_SERVICE_KEY")!);
        var destination = Side("destination", getVariable("SYNC_DEST_DB")!, getVariable("SYNC_DEST_SUPABASE_URL")!, getVariable("SYNC_DEST_SERVICE_KEY")!);

        if (string.Equals(source.ProjectRef, destination.ProjectRef, StringComparison.OrdinalIgnoreCase))
        {
            throw new SettingsException(
                $"Source and destination are the same project ({source.ProjectRef}). Refusing to run.");
        }

        if (SameDatabase(source.ConnectionString, destination.ConnectionString))
        {
            throw new SettingsException(
                $"Source and destination connect to the same database ({source.DbHost}). Refusing to run.");
        }

        return (source, destination);
    }

    private static Endpoint Side(string name, string connectionString, string supabaseUrl, string serviceKey)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            // Npgsql's message can quote the string; this one cannot.
            throw new SettingsException(
                $"The {name} database connection string is not in key=value form " +
                "(Host=...;Port=...;Username=...). Use Supabase's .NET connection string, not the postgresql:// URI.");
        }

        var storage = new StorageUrls(supabaseUrl);
        var urlRef = RefFromProjectUrl(storage.ProjectUrl)
            ?? throw new SettingsException($"The {name} Supabase URL is not https://<ref>.supabase.co.");
        var dbRef = RefFromDatabase(builder.Host, builder.Username)
            ?? throw new SettingsException(
                $"Cannot tell which Supabase project the {name} database ({builder.Host}) belongs to: " +
                "expected a pooler username postgres.<ref> or a host db.<ref>.supabase.co.");

        // A database of one project with the bucket of another would copy rows
        // and rewrite URLs against the wrong storage.
        if (!string.Equals(urlRef, dbRef, StringComparison.OrdinalIgnoreCase))
        {
            throw new SettingsException(
                $"The {name} database belongs to project {dbRef} but its Supabase URL to {urlRef}. Refusing to run.");
        }

        return new Endpoint(name, connectionString, storage, serviceKey, urlRef, builder.Host ?? "(none)");
    }

    /// <summary>https://abcd.supabase.co → abcd.</summary>
    internal static string? RefFromProjectUrl(string projectUrl)
    {
        if (!Uri.TryCreate(projectUrl, UriKind.Absolute, out var uri)
            || !uri.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var label = uri.Host[..^".supabase.co".Length];
        return label.Length > 0 && !label.Contains('.') ? label : null;
    }

    /// <summary>
    /// The session/transaction pooler shares one host per region between all
    /// projects, so the project is in the username (postgres.abcd). A direct
    /// connection carries it in the host (db.abcd.supabase.co).
    /// </summary>
    internal static string? RefFromDatabase(string? host, string? username)
    {
        if (username is not null)
        {
            var dot = username.IndexOf('.');
            if (dot > 0 && dot < username.Length - 1)
            {
                return username[(dot + 1)..];
            }
        }

        if (host is not null
            && host.StartsWith("db.", StringComparison.OrdinalIgnoreCase)
            && host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase))
        {
            var label = host[3..^".supabase.co".Length];
            return label.Length > 0 && !label.Contains('.') ? label : null;
        }

        return null;
    }

    private static bool SameDatabase(string a, string b)
    {
        var x = new NpgsqlConnectionStringBuilder(a);
        var y = new NpgsqlConnectionStringBuilder(b);
        return string.Equals(x.Host, y.Host, StringComparison.OrdinalIgnoreCase)
            && x.Port == y.Port
            && string.Equals(x.Database, y.Database, StringComparison.Ordinal)
            && string.Equals(x.Username, y.Username, StringComparison.Ordinal);
    }
}

public sealed class SettingsException(string message) : Exception(message);
