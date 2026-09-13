using Klowee.Api.Services;

namespace Klowee.Api.Tests;

/// <summary>
/// Lets a test decide what day it is, so "current menu" assertions do not drift
/// with the calendar or with the machine's time zone.
/// </summary>
public class FakeClock : IClock
{
    public DateOnly Today { get; set; } = new(2026, 4, 10);
}

/// <summary>
/// A <see cref="TimeProvider"/> pinned to one UTC instant, so the real
/// <see cref="SystemClock"/> can be exercised without waiting for a particular
/// moment of the day.
/// </summary>
public class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;
}
