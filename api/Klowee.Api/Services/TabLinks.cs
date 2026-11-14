using System.Buffers.Text;
using System.Security.Cryptography;

namespace Klowee.Api.Services;

/// <summary>
/// Capability links (decision 021): the token is the key, so it is 32 bytes
/// from the operating system's cryptographic generator (256 bits: unguessable),
/// encoded base64url so it sits in a URL as is.
/// </summary>
public static class TabLinks
{
    public static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Url(string publicSiteUrl, string token) => $"{publicSiteUrl.TrimEnd('/')}/tab/{token}";

    /// <summary>A request path with any tab token replaced, for logs: tokens are never logged.</summary>
    public static string Redact(string? path) =>
        path is null ? string.Empty
        : path.StartsWith("/api/public/tab/", StringComparison.OrdinalIgnoreCase)
            ? "/api/public/tab/[token]" + RestAfterToken(path)
            : path;

    private static string RestAfterToken(string path)
    {
        var rest = path["/api/public/tab/".Length..];
        var slash = rest.IndexOf('/');
        return slash < 0 ? string.Empty : rest[slash..];
    }
}
