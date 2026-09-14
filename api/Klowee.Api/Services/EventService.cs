using Klowee.Api.Common;
using Klowee.Api.Contracts.Events;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

public class EventService : IEventService
{
    private readonly KloweeDbContext _db;
    private readonly IClock _clock;

    public EventService(KloweeDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    // Ordering is applied to the entity query, before the projection, so it
    // survives translation no matter what the DTO carries.
    public async Task<IReadOnlyList<EventSummaryDto>> ListAsync(
        EventStatus? status,
        bool? published,
        CancellationToken cancellationToken) =>
        await Summaries(_db.Events
                .AsNoTracking()
                .Where(e => status == null || e.Status == status)
                .Where(e => published == null || e.IsPublished == published)
                .OrderByDescending(e => e.StartsOn)
                .ThenBy(e => e.Name))
            .ToListAsync(cancellationToken);

    public async Task<EventDetailDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await Details(_db.Events.AsNoTracking().Where(e => e.Id == id))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw NotFoundException.For("Event", id);

    public async Task<EventSummaryDto?> GetNextAsync(CancellationToken cancellationToken)
    {
        // "Still coming up" is measured in Cagayan de Oro, not UTC, and against
        // ends_on: a market that runs Thursday to Sunday is still the next event
        // on the Saturday. See docs/decisions/006-business-timezone.md.
        var today = _clock.Today;

        return await Summaries(_db.Events
                .AsNoTracking()
                .Where(e => e.IsPublished && e.Status == EventStatus.Upcoming && e.EndsOn >= today)
                .OrderBy(e => e.StartsOn)
                .ThenBy(e => e.Id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EventDetailDto> CreateAsync(EventRequest request, CancellationToken cancellationToken)
    {
        EnsureDatesMakeSense(request);

        var @event = new Event
        {
            Name = request.Name.Trim(),
            Venue = request.Venue.Trim(),
            Address = request.Address?.Trim(),
            StartsOn = request.StartsOn,
            EndsOn = request.EndsOn,
            Description = request.Description?.Trim(),
            CoverPhotoUrl = request.CoverPhotoUrl,
            Status = request.Status,
            IsPublished = false // new events are always drafts
        };

        _db.Events.Add(@event);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(@event.Id, cancellationToken);
    }

    public async Task<EventDetailDto> UpdateAsync(
        Guid id,
        EventRequest request,
        CancellationToken cancellationToken)
    {
        EnsureDatesMakeSense(request);

        var @event = await LoadAsync(id, cancellationToken);

        @event.Name = request.Name.Trim();
        @event.Venue = request.Venue.Trim();
        @event.Address = request.Address?.Trim();
        @event.StartsOn = request.StartsOn;
        @event.EndsOn = request.EndsOn;
        @event.Description = request.Description?.Trim();
        @event.CoverPhotoUrl = request.CoverPhotoUrl;
        @event.Status = request.Status;

        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Publishing is gated on status: a Planned event has no confirmed date to
    /// show and a Cancelled one should not be advertised. Unpublishing is always
    /// allowed, and both directions are idempotent.
    /// </summary>
    public async Task<EventDetailDto> SetPublishedAsync(
        Guid id,
        bool isPublished,
        CancellationToken cancellationToken)
    {
        var @event = await LoadAsync(id, cancellationToken);

        if (isPublished && @event.Status is not (EventStatus.Upcoming or EventStatus.Done))
        {
            throw new ConflictException(
                $"Event '{@event.Name}' is {@event.Status}. Planned and Cancelled events can't be published — " +
                "set the status to Upcoming or Done first.");
        }

        @event.IsPublished = isPublished;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Replaces the whole photo list in one transaction — the same shape as menu
    /// version items and package inclusions.
    /// </summary>
    public async Task<EventDetailDto> ReplacePhotosAsync(
        Guid id,
        IReadOnlyList<EventPhotoRequest> photos,
        CancellationToken cancellationToken)
    {
        await LoadAsync(id, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await _db.EventPhotos
            .Where(p => p.EventId == id)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var row in existing)
        {
            row.DeletedAt = now;
        }

        foreach (var photo in photos)
        {
            _db.EventPhotos.Add(new EventPhoto
            {
                EventId = id,
                Url = photo.Url.Trim(),
                Caption = photo.Caption?.Trim(),
                SortOrder = photo.SortOrder
            });
        }

        // One save: photos carry no partial unique index, so a replacement row
        // cannot collide with the tombstone of the row it replaces.
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var @event = await LoadAsync(id, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        @event.DeletedAt = now;

        var photos = await _db.EventPhotos
            .Where(p => p.EventId == id)
            .ToListAsync(cancellationToken);

        foreach (var photo in photos)
        {
            photo.DeletedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<EventSummaryDto> Summaries(IQueryable<Event> events) =>
        events.Select(e => new EventSummaryDto(
            e.Id, e.Name, e.Venue, e.StartsOn, e.EndsOn, e.Status, e.IsPublished,
            e.CoverPhotoUrl, e.Photos.Count));

    private static IQueryable<EventDetailDto> Details(IQueryable<Event> events) =>
        events.Select(e => new EventDetailDto(
            e.Id, e.Name, e.Venue, e.Address, e.StartsOn, e.EndsOn, e.Description,
            e.CoverPhotoUrl, e.Status, e.IsPublished,
            e.Photos
                .OrderBy(p => p.SortOrder)
                .Select(p => new EventPhotoDto(p.Id, p.Url, p.Caption, p.SortOrder))
                .ToList()));

    private static void EnsureDatesMakeSense(EventRequest request)
    {
        if (request.EndsOn < request.StartsOn)
        {
            throw new ValidationFailedException(
                $"endsOn ({request.EndsOn:yyyy-MM-dd}) is before startsOn ({request.StartsOn:yyyy-MM-dd}). " +
                "A one-day event repeats the same date.");
        }
    }

    private async Task<Event> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Events.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
        ?? throw NotFoundException.For("Event", id);
}
