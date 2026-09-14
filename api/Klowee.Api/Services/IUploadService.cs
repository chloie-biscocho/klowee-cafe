using Klowee.Api.Contracts.Media;

namespace Klowee.Api.Services;

public interface IUploadService
{
    Task<UploadResultDto> UploadAsync(
        string folder,
        string? declaredContentType,
        string? fileName,
        long length,
        Stream content,
        CancellationToken cancellationToken);

    Task DeleteAsync(string path, CancellationToken cancellationToken);
}
