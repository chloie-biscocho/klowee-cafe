using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Klowee.Api.Common;
using Microsoft.Extensions.Options;

namespace Klowee.Api.Services;

/// <summary>
/// Supabase Storage over its REST API, with a plain <see cref="HttpClient"/> —
/// no SDK. Three routes are all this needs:
///   POST   /storage/v1/object/{bucket}/{path}          upload
///   DELETE /storage/v1/object/{bucket}/{path}          remove
///   GET    /storage/v1/object/public/{bucket}/{path}   the public URL
/// Authentication is the service role key, sent as both a bearer token and the
/// <c>apikey</c> header, which is what the platform's gateway expects.
/// </summary>
public class SupabaseStorageService : IStorageService
{
    /// <summary>Named client, configured with the base address in Program.cs.</summary>
    public const string HttpClientName = "supabase-storage";

    private readonly HttpClient _http;
    private readonly SupabaseOptions _options;
    private readonly ILogger<SupabaseStorageService> _logger;

    public SupabaseStorageService(
        IHttpClientFactory httpClientFactory,
        IOptions<SupabaseOptions> options,
        ILogger<SupabaseStorageService> logger)
    {
        _http = httpClientFactory.CreateClient(HttpClientName);
        _options = options.Value;
        _logger = logger;
    }

    public async Task<StoredObject> UploadAsync(
        string path,
        string contentType,
        Stream content,
        long size,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"storage/v1/object/{_options.Bucket}/{path}");

        Authorize(request);

        // The object body is the raw bytes: Supabase Storage takes the file
        // itself, not a multipart envelope, when the Content-Type is the
        // object's own type.
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Headers.Add("x-upsert", "false");

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSucceededAsync(response, "upload", path, cancellationToken);

        return new StoredObject(PublicUrl(path), path, contentType, size);
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"storage/v1/object/{_options.Bucket}/{path}");

        Authorize(request);

        using var response = await _http.SendAsync(request, cancellationToken);

        // Deleting something that is already gone is a success, not a problem.
        if (await IsAlreadyGoneAsync(response, cancellationToken))
        {
            return;
        }

        await EnsureSucceededAsync(response, "delete", path, cancellationToken);
    }

    /// <summary>
    /// Supabase has answered a missing object two ways: a plain HTTP 404, and
    /// (since late 2026) an HTTP 400 whose body says
    /// <c>{"statusCode":"404","code":"NoSuchKey"}</c>. Both mean the same thing.
    /// Any other 400 is a real failure and must still surface.
    /// </summary>
    private static async Task<bool> IsAlreadyGoneAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return true;
        }

        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
            return false;
        }

        // Buffered, so EnsureSucceededAsync can still read the body for the log.
        await response.Content.LoadIntoBufferAsync(cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && ((root.TryGetProperty("code", out var code)
                        && code.ValueKind == JsonValueKind.String
                        && code.GetString() == "NoSuchKey")
                    || (root.TryGetProperty("statusCode", out var status)
                        && status.ValueKind == JsonValueKind.String
                        && status.GetString() == "404"));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string PublicUrl(string path) =>
        $"{_options.ProjectUrl}/storage/v1/object/public/{_options.Bucket}/{path}";

    private void Authorize(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);
        request.Headers.Add("apikey", _options.ServiceRoleKey);
    }

    private async Task EnsureSucceededAsync(
        HttpResponseMessage response,
        string operation,
        string path,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // The storage error body can name the bucket and the key. It goes to the
        // log, never to the caller.
        _logger.LogError(
            "Supabase Storage {Operation} of {Path} failed with {StatusCode}: {Body}",
            operation, path, (int)response.StatusCode, body);

        throw new StorageException($"The file could not be {(operation == "upload" ? "uploaded" : "removed")}.");
    }
}
