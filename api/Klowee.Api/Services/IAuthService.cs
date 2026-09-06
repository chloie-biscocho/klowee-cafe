using Klowee.Api.Contracts.Auth;

namespace Klowee.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>Resolves the caller of the current request from their token's "sub" claim.</summary>
    Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken);
}
