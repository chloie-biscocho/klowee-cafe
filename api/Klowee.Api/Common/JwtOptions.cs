namespace Klowee.Api.Common;

/// <summary>
/// Signing and validation settings for the access tokens this API issues.
/// Bound from the "Jwt" configuration section; the key itself lives in
/// user-secrets (development) or an environment variable (production).
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Symmetric signing key. Must be at least 32 characters (256 bits) for HS256.</summary>
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "klowee-cafe";

    public string Audience { get; set; } = "klowee-cafe";

    /// <summary>Access token lifetime. No refresh tokens in this phase.</summary>
    public int AccessTokenHours { get; set; } = 8;
}
