namespace Klowee.Api.Common;

/// <summary>
/// Limits on the public booking form (handover 20), per client IP (the real
/// one, from CF-Connecting-IP, as for login and the tab page): both windows
/// apply at once. Options so tests can loosen one to test the other.
/// </summary>
public class BookingRateLimitOptions
{
    public const string PolicyName = "booking";

    /// <summary>What a limited visitor reads.</summary>
    public const string RejectedMessage =
        "You've sent a few requests already. Please try again later, or message Klowee on Instagram or Facebook.";

    public int PerHour { get; set; } = 3;
    public int PerDay { get; set; } = 10;

    /// <summary>The two windows' lengths; tests shrink them to seconds.</summary>
    public TimeSpan HourWindow { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan DayWindow { get; set; } = TimeSpan.FromDays(1);
}
