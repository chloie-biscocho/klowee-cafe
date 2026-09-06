using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

public enum MenuContext
{
    Office,
    PopUp
}

/// <summary>
/// A named snapshot of a menu for a given context (office or pop-up) and an
/// effective date. Prices/availability are held per item on
/// <see cref="MenuVersionItem"/>.
/// </summary>
public class MenuVersion : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public MenuContext Context { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public bool IsPublished { get; set; }
    public string? Notes { get; set; }

    public ICollection<MenuVersionItem> Items { get; set; } = new List<MenuVersionItem>();
}
