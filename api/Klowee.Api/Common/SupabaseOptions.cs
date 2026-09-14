namespace Klowee.Api.Common;

/// <summary>
/// Where uploaded media goes. Bound from the "Supabase" configuration section;
/// the service role key lives in user-secrets locally and in an environment
/// variable in production, never in a committed file.
/// </summary>
public class SupabaseOptions
{
    public const string SectionName = "Supabase";

    /// <summary>
    /// Project URL, e.g. https://abcd.supabase.co. The Supabase dashboard shows
    /// several URLs on the same page, so a REST or Storage endpoint pasted here
    /// by mistake is normalised away by <see cref="ProjectUrl"/>.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Service role key. It bypasses row-level security, so it must never reach
    /// a browser — see docs/decisions/009-media-storage.md.
    /// </summary>
    public string ServiceRoleKey { get; set; } = string.Empty;

    /// <summary>Public storage bucket the media is written to.</summary>
    public string Bucket { get; set; } = "media";

    /// <summary>
    /// <see cref="Url"/> reduced to the project root, with no trailing slash.
    /// Storage lives at <c>/storage/v1</c> on that root; a configured value that
    /// already points at <c>/rest/v1</c> or <c>/storage/v1</c> would otherwise
    /// produce <c>/rest/v1/storage/v1/...</c>, which answers with a PostgREST
    /// error that says nothing about storage.
    /// </summary>
    public string ProjectUrl
    {
        get
        {
            var trimmed = Url.TrimEnd('/');

            foreach (var suffix in (string[])["/rest/v1", "/storage/v1", "/auth/v1"])
            {
                if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed[..^suffix.Length];
                }
            }

            return trimmed;
        }
    }
}
