using Klowee.Api.Common;
using Klowee.Api.Contracts.Media;
using ImageMagick;

namespace Klowee.Api.Services;

/// <summary>
/// Every rule about what may be uploaded lives here, so the controller stays a
/// controller and the storage class stays a transport. See
/// docs/decisions/009-media-storage.md and 012-image-processing.md.
/// </summary>
public class UploadService : IUploadService
{
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Either side above this is refused before the pixels are decoded.</summary>
    public const int MaxInputDimension = 8000;

    /// <summary>The longest edge of what gets stored. Smaller images are never enlarged.</summary>
    public const int MaxStoredEdge = 1600;

    public const int WebpQuality = 82;

    private const string StoredContentType = "image/webp";

    /// <summary>
    /// The decoder for each accepted content type. Naming the format on every
    /// read stops ImageMagick sniffing the bytes and reaching for one of its
    /// other coders (TIFF, SVG, PDF and dozens more); none of that code should
    /// be reachable from an upload.
    /// </summary>
    private static readonly Dictionary<string, MagickFormat> Decoders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = MagickFormat.Jpeg,
        ["image/png"] = MagickFormat.Png,
        ["image/webp"] = MagickFormat.WebP
    };

    /// <summary>
    /// Process-wide ceilings, so a hostile file that slips past the dimension
    /// check still cannot take the container's memory. Width and height sit
    /// above <see cref="MaxInputDimension"/>: that check gives the readable 400,
    /// these are the backstop. Memory over the limit spills to a disk cache,
    /// which is capped too.
    /// </summary>
    static UploadService()
    {
        ResourceLimits.Width = 16_384;
        ResourceLimits.Height = 16_384;
        ResourceLimits.Area = (ulong)MaxInputDimension * MaxInputDimension;
        ResourceLimits.Memory = 256UL * 1024 * 1024;
        ResourceLimits.Disk = 512UL * 1024 * 1024;
    }

    /// <summary>The only places media may be written, and the only prefixes a delete accepts.</summary>
    private static readonly string[] Folders = ["menu", "events", "site"];

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
        if (!Decoders.ContainsKey(contentType))
        {
            throw new ValidationFailedException(
                $"Only {string.Join(", ", Decoders.Keys)} files can be uploaded.");
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

        using var processed = await ProcessAsync(content, contentType, signature.AsSpan(0, read).ToArray(), cancellationToken);

        var today = _clock.Today;
        var path = $"{folder.ToLowerInvariant()}/{today:yyyy}/{today:MM}/{Guid.NewGuid():N}.webp";

        var stored = await _storage.UploadAsync(
            path, StoredContentType, processed, processed.Length, cancellationToken);

        return new UploadResultDto(stored.Url, stored.Path, stored.ContentType, stored.Size);
    }

    /// <summary>
    /// Whatever arrived (JPEG, PNG or WebP, any size up to the limits) leaves
    /// as an upright, metadata-free WebP no longer than <see cref="MaxStoredEdge"/>
    /// on its longest side.
    /// </summary>
    private static async Task<MemoryStream> ProcessAsync(
        Stream content,
        string contentType,
        byte[] alreadyRead,
        CancellationToken cancellationToken)
    {
        using var original = await BufferAsync(content, alreadyRead, cancellationToken);
        var settings = new MagickReadSettings
        {
            Format = Decoders[contentType],
            // An animated WebP becomes its first frame.
            FrameIndex = 0,
            FrameCount = 1
        };

        try
        {
            // MagickImageInfo reads the header only. A small file can still
            // declare 30000×30000 pixels, which would be gigabytes once decoded.
            var info = new MagickImageInfo(original, settings);
            if (info.Width > MaxInputDimension || info.Height > MaxInputDimension)
            {
                throw new ValidationFailedException(
                    $"The image is {info.Width}×{info.Height} pixels. Neither side may be above {MaxInputDimension}.");
            }

            original.Position = 0;
            using var image = new MagickImage();
            await image.ReadAsync(original, settings, cancellationToken);

            // A phone photo in Display P3 would look washed out if its profile
            // were simply dropped; convert the pixels to sRGB first. Returns
            // false, and does nothing, when there is no embedded profile.
            image.TransformColorSpace(ColorProfiles.SRGB);

            // Phones store pixels as the sensor saw them and record the
            // rotation in EXIF. Apply it now, because the EXIF is about to go.
            image.AutoOrient();

            // ">" in ImageMagick's geometry: only ever shrink, keeping the ratio.
            image.Resize(new MagickGeometry(MaxStoredEdge, MaxStoredEdge) { Greater = true });

            // Camera model, timestamps and, from phones, GPS coordinates. None
            // of it belongs on a public URL.
            image.Strip();

            image.Quality = WebpQuality;
            var output = new MemoryStream();
            await image.WriteAsync(output, MagickFormat.WebP, cancellationToken);
            output.Position = 0;
            return output;
        }
        catch (MagickException)
        {
            // The signature matched but the rest of the file did not hold up,
            // or it needed more than the resource limits allow.
            throw new ValidationFailedException("The image could not be read. It may be damaged.");
        }
    }

    /// <summary>
    /// The signature check has already consumed the first bytes. Rewind when the
    /// stream allows it; otherwise stitch those bytes back onto the rest.
    /// </summary>
    private static async Task<MemoryStream> BufferAsync(
        Stream content,
        byte[] alreadyRead,
        CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        if (content.CanSeek)
        {
            content.Position = 0;
        }
        else
        {
            buffer.Write(alreadyRead);
        }

        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
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
