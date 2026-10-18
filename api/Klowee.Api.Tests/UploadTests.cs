using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Media;
using Klowee.Api.Services;
using ImageMagick;
using ImageMagick.Drawing;

namespace Klowee.Api.Tests;

public class UploadTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public UploadTests(KloweeApiFactory factory) => _factory = factory;

    /// <summary>A real 1×1 PNG: signature, IHDR, IDAT, IEND.</summary>
    private static readonly byte[] ValidPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public async Task Upload_WithDisallowedContentType_Returns400()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.PostAsync(
            "/api/uploads?folder=menu",
            Multipart("notes.txt", "text/plain", "just some text"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("image/png", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_OverTheSizeLimit_Returns400()
    {
        var client = await _factory.CreateOwnerClientAsync();

        // Valid PNG bytes, then padding, so only the size rule can reject it.
        var oversize = new byte[UploadService.MaxBytes + 1];
        ValidPng.CopyTo(oversize, 0);

        var response = await client.PostAsync(
            "/api/uploads?folder=menu",
            Multipart("huge.png", "image/png", oversize));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("limit is 5 MB", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_WhereTheBytesDoNotMatchTheContentType_Returns400()
    {
        var client = await _factory.CreateOwnerClientAsync();

        // Says PNG, is not. The extension and the header are both the caller's
        // word; the signature is not.
        var notReallyAPng = "MZ\0\0this is an executable"u8.ToArray();

        var response = await client.PostAsync(
            "/api/uploads?folder=menu",
            Multipart("sneaky.png", "image/png", notReallyAPng));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not a valid image/png image", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(_factory.Storage.Uploaded, u => u.Path.EndsWith("sneaky.png"));
    }

    [Fact]
    public async Task Upload_WithAValidPng_StoresItAsWebpAndReturnsThePublicUrl()
    {
        _factory.Clock.Today = new DateOnly(2026, 9, 14);
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.PostAsync(
            "/api/uploads?folder=menu",
            Multipart("latte.png", "image/png", ValidPng));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResultDto>(KloweeApiFactory.Json);
        Assert.NotNull(result);
        Assert.Equal("image/webp", result!.ContentType);

        // The path is generated, never the caller's file name, and always .webp.
        Assert.StartsWith("menu/2026/09/", result.Path);
        Assert.EndsWith(".webp", result.Path);
        Assert.DoesNotContain("latte", result.Path);
        Assert.Equal($"{FakeStorageService.BaseUrl}/{result.Path}", result.Url);

        // The size reported is the size stored, not the size sent.
        var stored = Assert.Single(_factory.Storage.Uploaded, u => u.Path == result.Path);
        Assert.Equal(_factory.Storage.Contents[result.Path].Length, stored.Size);
        Assert.Equal(stored.Size, result.Size);
    }

    [Fact]
    public async Task Upload_ALargePng_IsResizedToTheLongEdgeLimitAndStoredAsWebp()
    {
        var client = await _factory.CreateOwnerClientAsync();
        var png = Encode(new MagickImage(new MagickColor("#2446C7"), 3000, 2000), MagickFormat.Png);

        var result = await UploadAsync(client, "big.png", "image/png", png);

        using var stored = new MagickImage(_factory.Storage.Contents[result.Path]);
        Assert.Equal(MagickFormat.WebP, stored.Format);
        Assert.Equal(1600u, stored.Width);
        Assert.Equal(1067u, stored.Height);
    }

    [Fact]
    public async Task Upload_ASmallJpeg_IsNotUpscaled()
    {
        var client = await _factory.CreateOwnerClientAsync();
        var jpeg = Encode(new MagickImage(new MagickColor("#F7F3E8"), 800, 600), MagickFormat.Jpeg);

        var result = await UploadAsync(client, "small.jpg", "image/jpeg", jpeg);

        using var stored = new MagickImage(_factory.Storage.Contents[result.Path]);
        Assert.Equal(MagickFormat.WebP, stored.Format);
        Assert.Equal(800u, stored.Width);
        Assert.Equal(600u, stored.Height);
    }

    [Fact]
    public async Task Upload_AnExifRotatedJpeg_IsStoredUprightWithoutMetadata()
    {
        var client = await _factory.CreateOwnerClientAsync();

        // A phone held upright: the sensor writes a landscape 400×200 frame and
        // records "rotate 90° clockwise to view" in EXIF. Red marks the sensor's
        // left edge, which is the top of the photo as the person saw it.
        var sensor = new MagickImage(MagickColors.White, 400, 200);
        new Drawables().FillColor(MagickColors.Red).Rectangle(0, 0, 39, 199).Draw(sensor);

        // ImageMagick only writes an EXIF profile that has a value in it, and on
        // write it syncs the tag from the image's Orientation, so set both.
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Orientation, (ushort)6); // 6 = RightTop: rotate 90° clockwise
        sensor.SetProfile(exif);
        sensor.Orientation = OrientationType.RightTop;
        sensor.Quality = 95;
        var jpeg = Encode(sensor, MagickFormat.Jpeg);

        var result = await UploadAsync(client, "portrait.jpg", "image/jpeg", jpeg);

        using var stored = new MagickImage(_factory.Storage.Contents[result.Path]);
        Assert.Equal(200u, stored.Width);
        Assert.Equal(400u, stored.Height);

        // Red is now along the top, and nothing tells a browser to rotate it again.
        using var pixels = stored.GetPixels();
        var top = pixels.GetPixel(100, 5).ToColor()!;
        var bottom = pixels.GetPixel(100, 395).ToColor()!;
        Assert.True(top.R > 200 && top.G < 80, $"top pixel was {top}");
        Assert.True(bottom.G > 200, $"bottom pixel was {bottom}");
        Assert.Null(stored.GetExifProfile());
    }

    [Fact]
    public async Task Upload_AnImageOverTheDimensionLimit_Returns400WithoutStoringIt()
    {
        var client = await _factory.CreateOwnerClientAsync();

        // One colour, so 9000×10 compresses to a few hundred bytes: the size
        // rule can't catch it, only the dimension rule can.
        var png = Encode(new MagickImage(new MagickColor("#FFD84D"), 9000, 10), MagickFormat.Png);
        var before = _factory.Storage.Uploaded.Count;

        var response = await client.PostAsync(
            "/api/uploads?folder=events",
            Multipart("panorama.png", "image/png", png));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("9000×10", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, _factory.Storage.Uploaded.Count);
    }

    [Fact]
    public async Task Upload_WithAValidSignatureButDamagedBody_Returns400()
    {
        var client = await _factory.CreateOwnerClientAsync();

        // The PNG signature, then nothing a decoder can use.
        var damaged = ValidPng[..8].Concat(new byte[64]).ToArray();

        var response = await client.PostAsync(
            "/api/uploads?folder=menu",
            Multipart("damaged.png", "image/png", damaged));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ToAnUnknownFolder_Returns400()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.PostAsync(
            "/api/uploads?folder=../secrets",
            Multipart("latte.png", "image/png", ValidPng));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithoutAToken_Returns401()
    {
        var client = await _factory.CreateAnonymousClientAsync();

        var response = await client.PostAsync(
            "/api/uploads?folder=menu",
            Multipart("latte.png", "image/png", ValidPng));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_OutsideTheAllowedFolders_Returns400()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.DeleteAsync("/api/uploads?path=../other-bucket/secret.png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(_factory.Storage.Deleted, path => path.Contains("secret.png"));
    }

    [Fact]
    public async Task Delete_InsideAnAllowedFolder_RemovesTheObject()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.DeleteAsync("/api/uploads?path=events/2026/09/abc.png");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("events/2026/09/abc.png", _factory.Storage.Deleted);
    }

    private async Task<UploadResultDto> UploadAsync(
        HttpClient client, string fileName, string contentType, byte[] bytes)
    {
        var response = await client.PostAsync("/api/uploads?folder=events", Multipart(fileName, contentType, bytes));
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResultDto>(KloweeApiFactory.Json);
        Assert.NotNull(result);
        Assert.Equal("image/webp", result!.ContentType);
        Assert.EndsWith(".webp", result.Path);
        return result;
    }

    private static byte[] Encode(MagickImage image, MagickFormat format)
    {
        using (image)
        {
            return image.ToByteArray(format);
        }
    }

    private static MultipartFormDataContent Multipart(string fileName, string contentType, byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}

/// <summary>
/// The Supabase dashboard shows the project URL, the REST endpoint and the
/// Storage endpoint together, so the wrong one lands in configuration easily.
/// </summary>
public class SupabaseOptionsTests
{
    [Theory]
    [InlineData("https://abcd.supabase.co", "https://abcd.supabase.co")]
    [InlineData("https://abcd.supabase.co/", "https://abcd.supabase.co")]
    [InlineData("https://abcd.supabase.co/rest/v1/", "https://abcd.supabase.co")]
    [InlineData("https://abcd.supabase.co/storage/v1", "https://abcd.supabase.co")]
    public void ProjectUrl_ReducesAnyOfTheDashboardUrlsToTheProjectRoot(string configured, string expected)
    {
        var options = new Klowee.Api.Common.SupabaseOptions { Url = configured };

        Assert.Equal(expected, options.ProjectUrl);
    }
}
