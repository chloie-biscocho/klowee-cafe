using Klowee.Api.Common;
using Klowee.Api.Contracts.Announcements;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

public class AnnouncementService : IAnnouncementService
{
    private readonly KloweeDbContext _db;

    public AnnouncementService(KloweeDbContext db) => _db = db;

    // Ordering and the window comparison happen in memory, not in SQL: EF Core's
    // SQLite provider - the one the test suite runs on - cannot order or compare
    // a DateTimeOffset column. Announcements are a banner at a time, a handful of
    // rows in total, so the cost is nothing and the rule stays testable. (The
    // same limitation is why menu versions tie-break on id; see handover 02.)
    public async Task<IReadOnlyList<AnnouncementDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await Project(_db.Announcements.AsNoTracking()).ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(a => a.ActiveFrom)
            .ThenByDescending(a => a.Id)
            .ToList();
    }

    public async Task<AnnouncementDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await Project(_db.Announcements.AsNoTracking().Where(a => a.Id == id))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw NotFoundException.For("Announcement", id);

    public async Task<AnnouncementDto?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        // An announcement is a banner with a window, so this is an instant
        // comparison, not a calendar-date one: UTC "now" is the right clock here
        // even though menu effective dates are not.
        var now = DateTimeOffset.UtcNow;

        // `is_active` narrows in SQL; the window is applied in memory for the
        // reason given on ListAsync.
        var active = await Project(_db.Announcements.AsNoTracking().Where(a => a.IsActive))
            .ToListAsync(cancellationToken);

        return active
            .Where(a => a.ActiveFrom <= now && (a.ActiveUntil == null || a.ActiveUntil > now))
            .OrderByDescending(a => a.ActiveFrom)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();
    }

    public async Task<AnnouncementDto> CreateAsync(
        AnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWindowMakesSense(request);

        var announcement = new Announcement
        {
            Text = request.Text.Trim(),
            LinkUrl = request.LinkUrl,
            ActiveFrom = Utc(request.ActiveFrom),
            ActiveUntil = Utc(request.ActiveUntil),
            IsActive = request.IsActive
        };

        _db.Announcements.Add(announcement);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(announcement.Id, cancellationToken);
    }

    public async Task<AnnouncementDto> UpdateAsync(
        Guid id,
        AnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWindowMakesSense(request);

        var announcement = await LoadAsync(id, cancellationToken);

        announcement.Text = request.Text.Trim();
        announcement.LinkUrl = request.LinkUrl;
        announcement.ActiveFrom = Utc(request.ActiveFrom);
        announcement.ActiveUntil = Utc(request.ActiveUntil);
        announcement.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<AnnouncementDto> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var announcement = await LoadAsync(id, cancellationToken);

        announcement.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var announcement = await LoadAsync(id, cancellationToken);

        announcement.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<AnnouncementDto> Project(IQueryable<Announcement> announcements) =>
        announcements.Select(a => new AnnouncementDto(
            a.Id, a.Text, a.LinkUrl, a.ActiveFrom, a.ActiveUntil, a.IsActive));

    /// <summary>
    /// Npgsql refuses to write a DateTimeOffset with a non-zero offset to a
    /// `timestamp with time zone` column, and the admin app naturally sends
    /// +08:00. Converting keeps the same instant — which is all timestamptz
    /// stores — and only normalises how it is expressed.
    /// </summary>
    private static DateTimeOffset Utc(DateTimeOffset value) => value.ToUniversalTime();

    private static DateTimeOffset? Utc(DateTimeOffset? value) => value?.ToUniversalTime();

    private static void EnsureWindowMakesSense(AnnouncementRequest request)
    {
        if (request.ActiveUntil is { } until && until <= request.ActiveFrom)
        {
            throw new ValidationFailedException(
                "activeUntil must be after activeFrom. Leave it out for an open-ended announcement.");
        }
    }

    private async Task<Announcement> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Announcements.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
        ?? throw NotFoundException.For("Announcement", id);
}
