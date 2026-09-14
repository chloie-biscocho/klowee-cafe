using Klowee.Api.Contracts.Events;
using Klowee.Api.Entities;

namespace Klowee.Api.Services;

public interface IEventService
{
    Task<IReadOnlyList<EventSummaryDto>> ListAsync(
        EventStatus? status,
        bool? published,
        CancellationToken cancellationToken);

    Task<EventDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// The event the public site puts at the top: published, Upcoming, not yet
    /// over in Manila, soonest first. Null when there is nothing coming up.
    /// </summary>
    Task<EventSummaryDto?> GetNextAsync(CancellationToken cancellationToken);

    Task<EventDetailDto> CreateAsync(EventRequest request, CancellationToken cancellationToken);
    Task<EventDetailDto> UpdateAsync(Guid id, EventRequest request, CancellationToken cancellationToken);
    Task<EventDetailDto> SetPublishedAsync(Guid id, bool isPublished, CancellationToken cancellationToken);
    Task<EventDetailDto> ReplacePhotosAsync(
        Guid id,
        IReadOnlyList<EventPhotoRequest> photos,
        CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
