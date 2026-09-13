namespace Klowee.Api.Services;

/// <summary>
/// The current business date. Injected rather than read from
/// <c>DateTime.UtcNow</c> so tests can fix "today" and so the answer is in the
/// business's time zone. See <c>docs/decisions/006-business-timezone.md</c>.
/// </summary>
public interface IClock
{
    /// <summary>Today's date in the configured business time zone.</summary>
    DateOnly Today { get; }
}
