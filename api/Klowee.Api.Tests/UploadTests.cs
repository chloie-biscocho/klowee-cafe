using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Media;
using Klowee.Api.Services;

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
    public async Task Upload_WithAValidPng_StoresItAndReturnsThePublicUrl()
    {
        _factory.Clock.Today = new DateOnly(2026, 9, 14);
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.PostAsync(
            "/api/uploads?folder=menu",
            Multipart("latte.png", "image/png", ValidPng));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResultDto>(KloweeApiFactory.Json);
        Assert.NotNull(result);
        Assert.Equal("image/png", result!.ContentType);
        Assert.Equal(ValidPng.Length, result.Size);

        // The path is generated, never the caller's file name.
        Assert.StartsWith("menu/2026/09/", result.Path);
        Assert.EndsWith(".png", result.Path);
        Assert.DoesNotContain("latte", result.Path);
        Assert.Equal($"{FakeStorageService.BaseUrl}/{result.Path}", result.Url);

        // The whole file reached storage, not just the bytes the signature check read.
        var stored = Assert.Single(_factory.Storage.Uploaded, u => u.Path == result.Path);
        Assert.Equal(ValidPng.Length, stored.Size);
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
