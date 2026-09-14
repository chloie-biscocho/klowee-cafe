namespace Klowee.Api.Contracts.Media;

/// <summary>What the caller gets back from <c>POST /api/uploads</c>.</summary>
/// <param name="Url">Public URL, safe to store on an entity and render on the site.</param>
/// <param name="Path">Object key inside the bucket; what <c>DELETE /api/uploads</c> takes.</param>
public record UploadResultDto(string Url, string Path, string ContentType, long Size);
