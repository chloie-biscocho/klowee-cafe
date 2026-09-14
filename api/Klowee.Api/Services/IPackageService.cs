using Klowee.Api.Contracts.Packages;

namespace Klowee.Api.Services;

public interface IPackageService
{
    Task<IReadOnlyList<PackageDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<PackageDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PackageDto> CreateAsync(PackageRequest request, CancellationToken cancellationToken);
    Task<PackageDto> UpdateAsync(Guid id, PackageRequest request, CancellationToken cancellationToken);
    Task<PackageDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
