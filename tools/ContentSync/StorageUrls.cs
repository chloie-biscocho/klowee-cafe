using System.Text.RegularExpressions;

namespace Klowee.ContentSync;

/// <summary>
/// Public object URLs of one Supabase project's media bucket:
/// <c>{project}/storage/v1/object/public/{bucket}/{path}</c>.
/// </summary>
public sealed class StorageUrls
{
    public const string Bucket = "media";

    private readonly string _publicPrefix;

    public StorageUrls(string projectUrl)
    {
        ProjectUrl = NormaliseProjectUrl(projectUrl);
        _publicPrefix = $"{ProjectUrl}/storage/v1/object/public/{Bucket}/";
    }

    /// <summary>The project root, e.g. https://abcd.supabase.co, with no trailing slash.</summary>
    public string ProjectUrl { get; }

    /// <summary>
    /// The shape of every path the API generates ({folder}/{yyyy}/{MM}/{guid}.{ext}),
    /// loosely: letters, digits, '_' and '-', '.' only after a segment's first
    /// character. No "." or ".." segment, no backslash, no percent-escape, no
    /// leading slash can match, so a path can never climb out of the bucket.
    /// </summary>
    private static readonly Regex SafePath = new(
        "^[A-Za-z0-9_-]+(/[A-Za-z0-9_-][A-Za-z0-9_.-]*)*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// The object path when <paramref name="url"/> is in this bucket; otherwise null.
    /// A URL that is in this bucket but whose path is not a plain object path
    /// (traversal, escapes) stops the run rather than being skipped: it would
    /// otherwise reach outside the bucket with the service key.
    /// </summary>
    public string? ObjectPath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !url.StartsWith(_publicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = url[_publicPrefix.Length..];
        // Strip a query string or fragment (a cache-buster, say): the object is the path.
        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            path = path[..cut];
        }

        if (path.Length == 0)
        {
            return null;
        }

        if (!SafePath.IsMatch(path))
        {
            throw new StorageCopyException($"A row refers to an unsafe object path in the {Bucket} bucket: {path}");
        }

        return path;
    }

    /// <summary>The path with each segment escaped, for building a request URI.</summary>
    public static string Escape(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    public string PublicUrl(string path) => _publicPrefix + path;

    /// <summary>
    /// A URL in the <paramref name="source"/> bucket becomes the same object
    /// path in <paramref name="destination"/>'s bucket. Anything else (null,
    /// empty, external, another project) comes back exactly as it was.
    /// </summary>
    public static string? Rewrite(string? url, StorageUrls source, StorageUrls destination)
    {
        var path = source.ObjectPath(url);
        return path is null ? url : destination.PublicUrl(path);
    }

    /// <summary>
    /// The dashboard shows the REST and Storage endpoints next to the project
    /// URL, so either may be pasted; both reduce to the project root (the same
    /// rule as the API's SupabaseOptions.ProjectUrl).
    /// </summary>
    public static string NormaliseProjectUrl(string url)
    {
        var trimmed = url.Trim().TrimEnd('/');
        foreach (var suffix in (string[])["/rest/v1", "/storage/v1", "/auth/v1"])
        {
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[..^suffix.Length];
            }
        }

        return trimmed;
    }
}
