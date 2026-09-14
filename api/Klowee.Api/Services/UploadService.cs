using Klowee.Api.Common;
using Klowee.Api.Contracts.Media;

namespace Klowee.Api.Services;

/// <summary>
/// Every rule about what may be uploaded lives here, so the controller stays a
/// controller and the storage class stays a transport. See
/// docs/decisions/009-media-storage.md.
/// </summary>
public class UploadService : IUploadService
{
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>The only places media may be written, and the only prefixes a delete accepts.</summary>
    private static readonly string[] Folders = ["menu", "events", "site"];

    /// <summary>Allowed content types and the extension each one is stored with.</summary>
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp"
    };

    private readonly IStorageService _storage;
    private readonly IClock _clock;

    public UploadService(IStorageService storage, IClock clock)
    {
        _storage = storage;
        _clock = clock;
    }

    public async Task<UploadResultDto> UploadAsync(
        string folder,
        string? declaredContentType,
        string? fileName,
        long length,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (!Folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationFailedException(
                $"folder must be one of: {string.Join(", ", Folders)}.");
        }

        var contentType = declaredContentType?.Split(';')[0].Trim() ?? string.Empty;
        if (!Extensions.TryGetValue(contentType, out var extension))
        {
            throw new ValidationFailedException(
                $"Only {string.Join(", ", Extensions.Keys)} files can be uploaded.");
        }

        if (length <= 0)
        {
            throw new ValidationFailedException("The file is empty.");
        }

        if (length > MaxBytes)
        {
            throw new ValidationFailedException(
                $"The file is {length / 1024 / 1024.0:0.0} MB. The limit is {MaxBytes / 1024 / 1024} MB.");
        }

        // The file name and the declared content type are both the caller's
        // word. The bytes are not: read the signature and make it agree.
        var signature = new byte[12];
        var read = await ReadAtLeastAsync(content, signature, cancellationToken);
        if (!MatchesSignature(contentType, signature.AsSpan(0, read)))
        {
            throw new ValidationFailedException(
                $"The file's contents are not a valid {contentType} image.");
        }

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var today = _clock.Today;
        var path = $"{folder.ToLowerInvariant()}/{today:yyyy}/{today:MM}/{Guid.NewGuid():N}.{extension}";

        var stored = await _storage.UploadAsync(path, contentType, content, length, cancellationToken);

        return new UploadResultDto(stored.Url, stored.Path, stored.ContentType, stored.Size);
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        var trimmed = path.Trim().TrimStart('/');

        // Without this, a path of "../../secrets" or another bucket's key would
        // be handed straight to storage.
        var folder = trimmed.Split('/').FirstOrDefault() ?? string.Empty;
        if (trimmed.Contains("..", StringComparison.Ordinal)
            || !Folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationFailedException(
                $"path must start with one of: {string.Join(", ", Folders)}.");
        }

        return _storage.DeleteAsync(trimmed, cancellationToken);
    }

    /// <summary>A stream may hand back fewer bytes than asked for; keep going until it stops.</summary>
    private static async Task<int> ReadAtLeastAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    /// <summary>
    /// Magic bytes: every image format starts with a fixed signature.
    ///   PNG   89 50 4E 47 0D 0A 1A 0A
    ///   JPEG  FF D8 FF
    ///   WebP  "RIFF" ���� "WEBP"  (bytes 0-3 and 8-11)
    /// </summary>
    private static bool MatchesSignature(string contentType, ReadOnlySpan<byte> head) => contentType switch
    {
        "image/png" => head.Length >= 8 && head[..8].SequenceEqual(PngSignature),
        "image/jpeg" => head.Length >= 3 && head[..3].SequenceEqual(JpegSignature),
        "image/webp" => head.Length >= 12
                        && head[..4].SequenceEqual("RIFF"u8)
                        && head[8..12].SequenceEqual("WEBP"u8),
        _ => false
    };

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];
}
