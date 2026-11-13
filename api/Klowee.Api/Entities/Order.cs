using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

/// <summary>Where an order was sold. Only office sales are recorded here; pop-ups live in Loyverse.</summary>
public enum OrderContext
{
    Office
}

public enum DiscountType
{
    None,
    Percent,
    Fixed,

    /// <summary>
    /// The old single card's free drink. Kept so history reads; new and edited
    /// orders can't use it (rewards now sit on lines, decision 020).
    /// </summary>
    LoyaltyFree
}

/// <summary>
/// One sale to one customer on one day. Subtotal, discount and total are
/// stored, and recomputed by the service whenever the order changes; payment
/// status is never stored, it is derived from the payments (decision 016).
/// </summary>
public class Order : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>Who settles this order's bill. Null: the customer pays ("Prince c/o Kee" sets Kee).</summary>
    public Guid? PayerId { get; set; }
    public Customer? Payer { get; set; }

    public OrderContext Context { get; set; } = OrderContext.Office;
    public DateOnly OrderedOn { get; set; }
    public string? Notes { get; set; }

    public DiscountType DiscountType { get; set; }

    /// <summary>Percent (0–100) or a peso amount; 0 for None and LoyaltyFree.</summary>
    public decimal DiscountValue { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
