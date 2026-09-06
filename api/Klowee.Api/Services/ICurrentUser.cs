namespace Klowee.Api.Services;

/// <summary>
/// The user behind the current request, read from the validated JWT. Injected
/// wherever business code needs the caller's identity without reaching for
/// HttpContext (which keeps services testable and HTTP-agnostic).
/// </summary>
public interface ICurrentUser
{
    /// <summary>The "sub" claim, or null when the request is anonymous.</summary>
    Guid? UserId { get; }

    /// <summary>The "email" claim, or null when the request is anonymous.</summary>
    string? Email { get; }

    bool IsAuthenticated { get; }
}
