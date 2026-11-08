using Klowee.Api.Contracts.Auth;

namespace Klowee.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>Resolves the caller of the current request from their token's "sub" claim.</summary>
    Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the caller's password after checking the current one. Tokens
    /// already issued stay valid until they expire (no revocation yet).
    /// </summary>
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken);
}
