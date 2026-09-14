using Klowee.Api.Contracts.Announcements;

namespace Klowee.Api.Services;

public interface IAnnouncementService
{
    Task<IReadOnlyList<AnnouncementDto>> ListAsync(CancellationToken cancellationToken);
    Task<AnnouncementDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The one the site shows right now: active, started, not yet expired,
    /// newest start first. Null when nothing is running.
    /// </summary>
    Task<AnnouncementDto?> GetCurrentAsync(CancellationToken cancellationToken);

    Task<AnnouncementDto> CreateAsync(AnnouncementRequest request, CancellationToken cancellationToken);
    Task<AnnouncementDto> UpdateAsync(Guid id, AnnouncementRequest request, CancellationToken cancellationToken);
    Task<AnnouncementDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
