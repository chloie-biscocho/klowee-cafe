namespace Klowee.Api.Common;

/// <summary>
/// Business-wide settings that are not secrets. Bound from the "App"
/// configuration section in appsettings.json.
/// </summary>
public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// IANA time zone the business operates in. Every "today" the API reasons
    /// about (menu effective dates, and later event and announcement windows)
    /// is resolved in this zone, not in UTC.
    /// </summary>
    public string TimeZone { get; set; } = "Asia/Manila";
}
