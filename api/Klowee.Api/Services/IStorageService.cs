namespace Klowee.Api.Services;

/// <summary>An object that now lives in the media bucket.</summary>
public record StoredObject(string Url, string Path, string ContentType, long Size);

/// <summary>
/// Object storage, reduced to the two operations this API needs. Behind an
/// interface so the upload rules can be tested without a network call and so
/// swapping Supabase Storage for something else touches one class.
/// </summary>
public interface IStorageService
{
    /// <param name="path">Object key inside the bucket, e.g. <c>menu/2026/09/{guid}.png</c>.</param>
    Task<StoredObject> UploadAsync(
        string path,
        string contentType,
        Stream content,
        long size,
        CancellationToken cancellationToken);

    Task DeleteAsync(string path, CancellationToken cancellationToken);
}
