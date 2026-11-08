using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

public enum UserRole
{
    Owner
}

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Owner;

    /// <summary>
    /// The start of the second after the password last changed. Tokens issued
    /// before it (their "iat") are rejected, so a change signs the user out everywhere.
    /// </summary>
    public DateTimeOffset? PasswordChangedAt { get; set; }
}
