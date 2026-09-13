using Klowee.Api.Common;
using Microsoft.Extensions.Options;

namespace Klowee.Api.Services;

/// <summary>
/// Resolves "today" as the calendar date in <see cref="AppOptions.TimeZone"/>.
/// The zone is looked up once at construction, so a bad id fails fast at
/// startup instead of on the first request that needs a date.
/// </summary>
public class SystemClock : IClock
{
    private readonly TimeZoneInfo _timeZone;
    private readonly TimeProvider _timeProvider;

    public SystemClock(IOptions<AppOptions> options, TimeProvider timeProvider)
    {
        var id = options.Value.TimeZone;

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"App:TimeZone '{id}' is not a time zone this machine knows about.", exception);
        }

        _timeProvider = timeProvider;
    }

    public DateOnly Today =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone).DateTime);
}
