namespace Klowee.Api.Common;

/// <summary>
/// Limits on the public tab page (handover 17). Reads by client IP; writes by
/// token, so one leaked link can't flood the owners with claims or requests.
/// Options so tests can tighten or loosen them.
/// </summary>
public class TabRateLimitOptions
{
    public const string ReadPolicy = "tab-read";
    public const string ClaimPolicy = "tab-claim";
    public const string CardRequestPolicy = "tab-card-request";

    public int ReadsPerMinute { get; set; } = 60;
    public int ClaimsPerHour { get; set; } = 5;
    public int CardRequestsPerDay { get; set; } = 3;
}
