using System.Security.Claims;

namespace Klowee.Api.Services;

/// <summary>
/// <see cref="ICurrentUser"/> backed by the ambient HttpContext. Registered as
/// scoped; outside a request (design-time tooling, the seeder) every property
/// is null, which is why <c>created_by</c> stays nullable.
/// </summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            // Inbound claim mapping is turned off (see Program.cs), so "sub"
            // arrives verbatim rather than as ClaimTypes.NameIdentifier.
            var value = Principal?.FindFirstValue("sub")
                        ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email =>
        Principal?.FindFirstValue("email") ?? Principal?.FindFirstValue(ClaimTypes.Email);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
}
