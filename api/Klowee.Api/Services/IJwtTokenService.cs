using Klowee.Api.Entities;

namespace Klowee.Api.Services;

public interface IJwtTokenService
{
    /// <summary>Issues a signed access token for the given user.</summary>
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user);
}
