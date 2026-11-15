using Klowee.Api.Common;
using Klowee.Api.Contracts.Bookings;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Data;
using Klowee.Api.Data.Configurations;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

public interface IBookingService
{
    /// <summary>
    /// A visitor's request from the homepage. Stores it, unless the honeypot is
    /// filled (then nothing, silently). 409 when the form is switched off;
    /// field errors for bad input.
    /// </summary>
    Task SubmitAsync(PublicBookingRequest request, CancellationToken cancellationToken);

    Task<PagedResult<BookingSummaryDto>> ListAsync(BookingStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<BookingDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<BookingDto> UpdateAsync(Guid id, UpdateBookingRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Booking requests (handover 20, decision 023). Anyone may send one; only
/// the owners can read them. The visitor's name, contact, venue and message
/// never reach a log: the request's id and the outcome do.
/// </summary>
public class BookingService : IBookingService
{
    public const string EnabledKey = "booking_form_enabled";
    public const string NoteKey = "booking_form_note";

    /// <summary>The Book section's numbered steps (handover 20a): one per line, at most three, each up to 80 characters.</summary>
    public const string StepsKey = "booking_steps";
    public const int MaxSteps = 3;
    public const int MaxStepLength = 80;

    /// <summary>The owners' approved wording, seeded once by the migration `SeedBookingSteps`.</summary>
    public const string DefaultSteps =
        "Send us your date and venue.\nWe message you back to talk through the details.\nWe confirm, then roll up with the cart.";

    /// <summary>
    /// What's wrong with a steps value, or null. Blank lines don't count (the
    /// site skips them), so the rule is about the steps a visitor would see.
    /// </summary>
    public static string? StepsProblem(string? value)
    {
        var steps = (value ?? string.Empty).Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        if (steps.Count > MaxSteps)
        {
            return $"Booking steps: at most {MaxSteps} lines, one step per line.";
        }

        return steps.Any(step => step.Length > MaxStepLength)
            ? $"Booking steps: each line up to {MaxStepLength} characters."
            : null;
    }

    private readonly KloweeDbContext _db;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;
    private readonly ILogger<BookingService> _logger;

    public BookingService(KloweeDbContext db, IClock clock, ICurrentUser user, ILogger<BookingService> logger)
    {
        _db = db;
        _clock = clock;
        _user = user;
        _logger = logger;
    }

    /// <summary>
    /// The form is on unless the setting says exactly "false" (any case). A
    /// missing or empty setting means on, so the form works before anyone
    /// opens Settings. The website reads the same rule.
    /// </summary>
    public static bool IsEnabled(string? value) =>
        !string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase);

    public async Task SubmitAsync(PublicBookingRequest request, CancellationToken cancellationToken)
    {
        var setting = await _db.SiteSettings.AsNoTracking()
            .Where(s => s.Key == EnabledKey).Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
        if (!IsEnabled(setting))
        {
            _logger.LogInformation("Booking request refused: the form is switched off");
            throw new ConflictException("Booking requests are switched off right now. Please message Klowee instead.");
        }

        // A bot fills every field it finds. It gets the same answer as a person, and nothing is kept.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            _logger.LogInformation("Booking request discarded: honeypot filled");
            return;
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var name = Required(request.Name, "name", "Please tell us your name.", BookingLimits.Name, errors);
        var contact = Required(request.Contact, "contact", "Please tell us how to reach you.", BookingLimits.Contact, errors);
        var venue = Required(request.Venue, "venue", "Please tell us where the event is.", BookingLimits.Venue, errors);
        var message = Optional(request.Message, "message", BookingLimits.Message, errors);

        var today = _clock.Today;
        if (request.EventDate is not { } date)
        {
            errors["eventDate"] = ["Please pick the event date."];
        }
        else if (date < today)
        {
            errors["eventDate"] = ["That date has passed. Please pick today or later."];
        }
        else if (date > today.AddYears(BookingLimits.MaxYearsAhead))
        {
            errors["eventDate"] = [$"Please pick a date within {BookingLimits.MaxYearsAhead} years."];
        }

        if (request.GuestCount is { } guests && (guests < BookingLimits.MinGuests || guests > BookingLimits.MaxGuests))
        {
            errors["guestCount"] = [$"Guests can be {BookingLimits.MinGuests} to {BookingLimits.MaxGuests:#,0}."];
        }

        string? packageName = null;
        if (request.PackageId is { } packageId)
        {
            packageName = await _db.Packages.AsNoTracking()
                .Where(p => p.Id == packageId && p.IsActive)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken);
            if (packageName is null)
            {
                errors["packageId"] = ["That package isn't available any more. Please pick another, or \"Not sure yet\"."];
            }
        }

        if (errors.Count > 0)
        {
            _logger.LogInformation("Booking request rejected: invalid {Fields}", string.Join(",", errors.Keys));
            throw new FieldValidationException(errors);
        }

        var booking = new BookingRequest
        {
            Name = name!,
            Contact = contact!,
            EventDate = request.EventDate!.Value,
            Venue = venue!,
            GuestCount = request.GuestCount,
            PackageId = request.PackageId,
            PackageName = packageName,
            Message = message,
            Status = BookingStatus.New
        };
        _db.BookingRequests.Add(booking);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Booking request {BookingId} stored", booking.Id);
    }

    public async Task<PagedResult<BookingSummaryDto>> ListAsync(BookingStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = await _db.BookingRequests
            .AsNoTracking()
            .Where(b => status == null || b.Status == status)
            .Select(b => new BookingSummaryDto(b.Id, b.EventDate, b.Name, b.Venue, b.GuestCount, b.PackageName, b.Status, b.CreatedAt))
            .ToListAsync(cancellationToken);

        // Newest first, ordered in memory (SQLite in the tests can't order by a
        // DateTimeOffset). A few requests a week: the whole filter is small.
        var ordered = rows.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id).ToList();
        var current = Math.Max(1, page);
        var size = Math.Clamp(pageSize, 1, 200);
        return new PagedResult<BookingSummaryDto>(ordered.Skip((current - 1) * size).Take(size).ToList(), current, size, ordered.Count);
    }

    public async Task<BookingDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var booking = await _db.BookingRequests.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Booking request", id);
        var changedBy = booking.StatusChangedBy is { } userId
            ? await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new BookingDto(
            booking.Id, booking.Name, booking.Contact, booking.EventDate, booking.Venue, booking.GuestCount,
            booking.PackageId, booking.PackageName, booking.Message, booking.Status, booking.OwnerNotes,
            booking.StatusChangedAt, changedBy, booking.CreatedAt);
    }

    public async Task<BookingDto> UpdateAsync(Guid id, UpdateBookingRequest request, CancellationToken cancellationToken)
    {
        var booking = await _db.BookingRequests.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Booking request", id);

        var status = request.Status!.Value;
        if (status != booking.Status)
        {
            // Who and when, only when the status really changes: saving notes stamps nothing.
            booking.Status = status;
            booking.StatusChangedAt = DateTimeOffset.UtcNow;
            booking.StatusChangedBy = _user.UserId;
        }

        booking.OwnerNotes = string.IsNullOrWhiteSpace(request.OwnerNotes) ? null : request.OwnerNotes.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Booking request {BookingId} updated: {Status}", booking.Id, booking.Status);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var booking = await _db.BookingRequests.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Booking request", id);
        booking.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Booking request {BookingId} deleted", booking.Id);
    }

    private static string? Required(string? value, string field, string missing, int max, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            errors[field] = [missing];
            return null;
        }

        if (trimmed.Length > max)
        {
            errors[field] = [$"Please keep this to {max} characters."];
            return null;
        }

        return trimmed;
    }

    private static string? Optional(string? value, string field, int max, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > max)
        {
            errors[field] = [$"Please keep this to {max} characters."];
            return null;
        }

        return trimmed;
    }
}
