using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

/// <summary>
/// A menu item holds identity only (name, description, category, photo).
/// Price and availability live on <see cref="MenuVersionItem"/>. See decision
/// record docs/decisions/002-menu-versioning.md.
/// </summary>
public class MenuItem : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }
    public MenuCategory? Category { get; set; }

    public string? PhotoUrl { get; set; }
    public bool IsArchived { get; set; }

    public ICollection<MenuVersionItem> VersionItems { get; set; } = new List<MenuVersionItem>();
}
