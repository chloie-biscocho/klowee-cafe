namespace Klowee.Api.Common;

/// <summary>
/// How many login attempts one IP gets per window (docs/decisions/013-hosting-and-config.md).
/// Not bound from configuration: the defaults are the policy. It is an options
/// class only so the integration tests, which all sign in from the same
/// address, can raise it.
/// </summary>
public class LoginRateLimitOptions
{
    public const string PolicyName = "login";

    public int PermitLimit { get; set; } = 5;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
