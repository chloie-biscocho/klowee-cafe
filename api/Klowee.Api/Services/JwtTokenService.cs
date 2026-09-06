using System.Text;
using Klowee.Api.Common;
using Klowee.Api.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Klowee.Api.Services;

/// <summary>
/// Issues the HS256 access tokens this API accepts. The same symmetric key is
/// used to sign here and to validate in Program.cs, so no database lookup is
/// needed to trust an incoming token.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(_options.AccessTokenHours);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id.ToString(),
                ["email"] = user.Email,
                ["name"] = user.DisplayName,
                ["role"] = user.Role.ToString()
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
                SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        // Trim to whole seconds so the reported expiry matches the token's own
        // "exp" claim, which has one-second resolution.
        return (token, new DateTimeOffset(expiresAt.UtcDateTime).AddTicks(-(expiresAt.UtcTicks % TimeSpan.TicksPerSecond)));
    }
}
