using System.Collections.Concurrent;
using Klowee.Api.Services;

namespace Klowee.Api.Tests;

/// <summary>
/// Stands in for Supabase Storage so the upload rules can be tested without a
/// network call. Records what it was asked to store, which is how a test checks
/// that a rejected upload never reached storage at all.
/// </summary>
public class FakeStorageService : IStorageService
{
    public const string BaseUrl = "https://fake.supabase.test/storage/v1/object/public/media";

    public ConcurrentBag<StoredObject> Uploaded { get; } = [];
    public ConcurrentBag<string> Deleted { get; } = [];

    public async Task<StoredObject> UploadAsync(
        string path,
        string contentType,
        Stream content,
        long size,
        CancellationToken cancellationToken)
    {
        // Drain the stream the way the real client would, so a test that forgets
        // to rewind it fails here rather than silently storing nothing.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        var stored = new StoredObject($"{BaseUrl}/{path}", path, contentType, buffer.Length);
        Uploaded.Add(stored);
        return stored;
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        Deleted.Add(path);
        return Task.CompletedTask;
    }
}
