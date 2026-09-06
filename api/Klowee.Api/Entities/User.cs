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
}
