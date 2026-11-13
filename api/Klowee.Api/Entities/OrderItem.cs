using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

/// <summary>
/// A drink on an order. Points at the exact priced row it was sold from
/// (decision 002) and copies that row's price: <see cref="UnitPrice"/> is set
/// once, at creation, and never recomputed.
/// </summary>
public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public Guid MenuVersionItemId { get; set; }
    public MenuVersionItem? MenuVersionItem { get; set; }

    public int Quantity { get; set; }

    /// <summary>The line's position on the order, as entered. Lines saved together share a created_at.</summary>
    public int SortOrder { get; set; }

    /// <summary>Snapshot of MenuVersionItem.Price when the order was taken.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>(UnitPrice + the add-ons' prices) × Quantity, less the line's reward.</summary>
    public decimal LineTotal { get; set; }

    /// <summary>The loyalty reward applied to one unit of this line, if any.</summary>
    public Guid? LoyaltyRedemptionId { get; set; }
    public LoyaltyRedemption? LoyaltyRedemption { get; set; }

    public ICollection<OrderItemAddOn> AddOns { get; set; } = new List<OrderItemAddOn>();
}
