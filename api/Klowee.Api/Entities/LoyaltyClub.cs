using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

/// <summary>Drives the icon and colour on the customer page.</summary>
public enum LoyaltyClubStyle
{
    Coffee,
    Matcha
}

/// <summary>
/// A loyalty card: a row of slots that paid drinks of the club fill, some of
/// which carry a reward. The card restarts after the last slot. A customer's
/// place on it is derived from the ledger, never stored (decision 020).
/// </summary>
public class LoyaltyClub : BaseEntity
{
    /// <summary>
    /// The two clubs the LoyaltyClubs migration creates, with fixed ids in every
    /// environment so a category copied by ContentSync points at the same club.
    /// </summary>
    public static readonly Guid CoffeeClubId = new("c0ffee00-0000-4000-8000-000000000001");

    public static readonly Guid MatchaClubId = new("3a7c4a00-0000-4000-8000-000000000002");

    public string Name { get; set; } = string.Empty;
    public LoyaltyClubStyle Style { get; set; }
    public int SlotCount { get; set; } = 12;

    /// <summary>An inactive club earns nothing and is not shown on customers.</summary>
    public bool IsActive { get; set; }

    /// <summary>First set when the club is activated; orders before this date never stamp it.</summary>
    public DateOnly? ActivatedOn { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// Members of this other club get this club's card automatically: when this
    /// club is activated, and whenever someone is given the other club's card.
    /// The Matcha Club auto-joins from The Coffee Club (handover 16a).
    /// </summary>
    public Guid? AutoJoinFromClubId { get; set; }
    public LoyaltyClub? AutoJoinFromClub { get; set; }

    public ICollection<LoyaltyClubReward> Rewards { get; set; } = new List<LoyaltyClubReward>();
}

public enum LoyaltyRewardType
{
    /// <summary>A percentage off one unit's drink price; add-ons charged in full.</summary>
    PercentOff,

    /// <summary>A named add-on on one unit at ₱0.</summary>
    FreeAddOn,

    /// <summary>One unit's drink price at ₱0; add-ons still charged.</summary>
    FreeDrink
}

/// <summary>The reward printed on one slot of a club's card. One per slot.</summary>
public class LoyaltyClubReward : BaseEntity
{
    public Guid ClubId { get; set; }
    public LoyaltyClub? Club { get; set; }

    /// <summary>1..SlotCount. Earned once the slot before it is filled.</summary>
    public int Slot { get; set; }

    public LoyaltyRewardType Type { get; set; }

    /// <summary>For PercentOff: 1–100.</summary>
    public decimal? Percent { get; set; }

    /// <summary>For FreeAddOn: the add-on given free.</summary>
    public Guid? AddOnId { get; set; }
    public AddOn? AddOn { get; set; }

    /// <summary>Short text printed on the slot ("50% OFF").</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// One reward used: by an order line, or marked by hand (no order). At most one
/// live row per (customer, club, cycle, reward), so a reward is used once per card.
/// </summary>
public class LoyaltyRedemption : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid ClubId { get; set; }
    public LoyaltyClub? Club { get; set; }

    public Guid RewardId { get; set; }
    public LoyaltyClubReward? Reward { get; set; }

    /// <summary>Which pass round the card, 0-based.</summary>
    public int Cycle { get; set; }

    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid? OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public string? Note { get; set; }
}

public enum MembershipStatus
{
    /// <summary>The customer asked for a card; not yet given. Earns nothing.</summary>
    Requested,

    /// <summary>Holds the card: paid drinks of the club stamp it from <c>JoinedOn</c>.</summary>
    Active
}

/// <summary>
/// A customer's card in one club. Cards are by invitation (handover 16a): only
/// an Active member's paid orders, dated on or after <see cref="JoinedOn"/>,
/// stamp the club. One live row per (customer, club).
/// </summary>
public class LoyaltyMembership : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid ClubId { get; set; }
    public LoyaltyClub? Club { get; set; }

    public MembershipStatus Status { get; set; }

    /// <summary>
    /// The first day orders stamp the card. Null while Requested; null on an
    /// Active membership means "since the beginning" (only the carry-over writes that).
    /// </summary>
    public DateOnly? JoinedOn { get; set; }

    public string? Note { get; set; }
}

/// <summary>Which club a menu item's drinks stamp.</summary>
public enum LoyaltyMode
{
    /// <summary>Its category's club, if any.</summary>
    Inherit,

    /// <summary>No club: earns nothing.</summary>
    None,

    /// <summary>The item's own club (MenuItem.LoyaltyClubId).</summary>
    Club
}

/// <summary>
/// A one-time data step that has run (the loyalty carry-over). The row is the
/// marker: startup skips a step whose name is already here.
/// </summary>
public class DataMigration : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Summary { get; set; }
}
