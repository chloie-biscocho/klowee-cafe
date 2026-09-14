using Klowee.Api.Common;
using Klowee.Api.Contracts.Settings;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

public class SiteSettingService : ISiteSettingService
{
    /// <summary>
    /// The settings the site knows how to render. A closed list on purpose: a
    /// typo in a key would otherwise create a row nothing reads, and the page
    /// would quietly keep its old copy.
    /// </summary>
    public static readonly IReadOnlySet<string> KnownKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "hero_heading",
        "hero_body",
        "hero_image_url",
        "story_heading",
        "story_body",
        "story_image_url",
        "ticker_fallback",
        "instagram_url",
        "facebook_url",
        "contact_email"
    };

    private readonly KloweeDbContext _db;

    public SiteSettingService(KloweeDbContext db) => _db = db;

    public async Task<IReadOnlyList<SiteSettingDto>> ListAsync(CancellationToken cancellationToken) =>
        await _db.SiteSettings
            .AsNoTracking()
            .OrderBy(s => s.Key)
            .Select(s => new SiteSettingDto(s.Key, s.Value))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SiteSettingDto>> UpsertAsync(
        IReadOnlyList<SiteSettingRequest> settings,
        CancellationToken cancellationToken)
    {
        var unknown = settings
            .Select(s => s.Key)
            .Where(key => !KnownKeys.Contains(key))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (unknown.Count > 0)
        {
            throw new ValidationFailedException(
                $"Unknown setting key(s): {string.Join(", ", unknown)}. " +
                $"Known keys are: {string.Join(", ", KnownKeys.Order())}.");
        }

        var duplicate = settings
            .GroupBy(s => s.Key, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ValidationFailedException($"Setting '{duplicate.Key}' appears more than once.");
        }

        var keys = settings.Select(s => s.Key).ToList();
        var existing = await _db.SiteSettings
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, cancellationToken);

        foreach (var setting in settings)
        {
            // The converter in Common/EmptyStringToNullConverter.cs turns an
            // empty box into null; for a setting that means "clear it".
            var value = setting.Value ?? string.Empty;

            if (existing.TryGetValue(setting.Key, out var row))
            {
                row.Value = value;
            }
            else
            {
                _db.SiteSettings.Add(new SiteSetting { Key = setting.Key, Value = value });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await ListAsync(cancellationToken);
    }
}
