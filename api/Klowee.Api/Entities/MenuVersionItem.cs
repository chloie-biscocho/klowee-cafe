using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

/// <summary>
/// Joins a <see cref="MenuVersion"/> to a <see cref="MenuItem"/> and carries the
/// per-version price and availability. Future orders will reference this row so
/// historical prices are preserved. Unique per (version, item).
/// </summary>
public class MenuVersionItem : BaseEntity
{
    public Guid MenuVersionId { get; set; }
    public MenuVersion? MenuVersion { get; set; }

    public Guid MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }

    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
}
