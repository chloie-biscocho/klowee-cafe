using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

public class MenuCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    /// <summary>An optional line shown under the name on the public menu ("oat milk + ceremonial matcha…").</summary>
    public string? Description { get; set; }

    /// <summary>The club this category's drinks stamp, unless an item overrides it. Null: no club.</summary>
    public Guid? LoyaltyClubId { get; set; }
    public LoyaltyClub? LoyaltyClub { get; set; }

    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}
