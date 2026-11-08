using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

public enum LoyaltyReason
{
    Earned,
    Redeemed,
    Adjustment
}

/// <summary>
/// One line of a customer's stamp ledger. The balance is the sum of the live
/// entries' deltas; nothing stores it. An entry tied to an order is voided
/// (soft-deleted) when the order stops qualifying for it (decision 016).
/// </summary>
public class LoyaltyEntry : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>Positive for stamps earned, negative for a redemption.</summary>
    public int Delta { get; set; }

    public LoyaltyReason Reason { get; set; }
    public string? Notes { get; set; }
}
