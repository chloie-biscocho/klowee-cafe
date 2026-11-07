using System.Net;
using System.Net.Http.Headers;

namespace Klowee.ContentSync;

/// <summary>
/// The media bucket of one project, over Supabase Storage's REST API with the
/// service role key: the same approach as the API's SupabaseStorageService
/// (decision 009), plus the two calls a copy needs, size and download.
/// </summary>
public sealed class StorageClient : IDisposable
{
    private readonly HttpClient _http;

    public StorageClient(Endpoint endpoint)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(endpoint.Storage.ProjectUrl + "/"),
            Timeout = TimeSpan.FromSeconds(60)
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.ServiceKey);
        _http.DefaultRequestHeaders.Add("apikey", endpoint.ServiceKey);
    }

    /// <summary>The object's size in bytes, or null when it does not exist.</summary>
    public async Task<long?> SizeAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, $"storage/v1/object/authenticated/{StorageUrls.Bucket}/{path}");
        using var response = await _http.SendAsync(request, cancellationToken);

        // Missing has been both a 404 and a 400 ("NoSuchKey"), see decision 009.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            return null;
        }

        await EnsureSucceededAsync(response, "check", path);
        return response.Content.Headers.ContentLength;
    }

    public async Task<(byte[] Bytes, string ContentType)> DownloadAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync($"storage/v1/object/authenticated/{StorageUrls.Bucket}/{path}", cancellationToken);
        await EnsureSucceededAsync(response, "download", path);

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return (bytes, contentType);
    }

    /// <summary>Writes the object, replacing one of a different size at the same path.</summary>
    public async Task UploadAsync(string path, byte[] bytes, string contentType, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"storage/v1/object/{StorageUrls.Bucket}/{path}");
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Headers.Add("x-upsert", "true");

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSucceededAsync(response, "upload", path);
    }

    private static async Task EnsureSucceededAsync(HttpResponseMessage response, string operation, string path)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // The body is Supabase's error JSON: bucket and key at most, no secrets.
        var body = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        throw new StorageCopyException(
            $"Storage {operation} of {path} failed with HTTP {(int)response.StatusCode}: {Truncate(body)}");
    }

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300] + "…";

    public void Dispose() => _http.Dispose();
}

public sealed class StorageCopyException(string message) : Exception(message);
