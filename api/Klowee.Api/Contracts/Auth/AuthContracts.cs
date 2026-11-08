using System.ComponentModel.DataAnnotations;

namespace Klowee.Api.Contracts.Auth;

/// <summary>Credentials posted to <c>POST /api/auth/login</c>.</summary>
public record LoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

/// <summary>The signed-in owner. Never exposes the password hash.</summary>
public record UserDto(Guid Id, string Email, string DisplayName);

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

/// <summary>Body of <c>PUT /api/auth/password</c>.</summary>
public record ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    [MinLength(12, ErrorMessage = "The new password must be at least 12 characters.")]
    [MaxLength(200)]
    public string NewPassword { get; init; } = string.Empty;
}
