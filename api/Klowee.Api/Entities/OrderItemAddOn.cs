using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

/// <summary>An extra on one drink (extra shot, oat milk), at the price charged then.</summary>
public class OrderItemAddOn : BaseEntity
{
    public Guid OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public Guid AddOnId { get; set; }
    public AddOn? AddOn { get; set; }

    /// <summary>Snapshot of AddOn.Price when the order was taken. Applies to each unit of the drink.</summary>
    public decimal UnitPrice { get; set; }
}
