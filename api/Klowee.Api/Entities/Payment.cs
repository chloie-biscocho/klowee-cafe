using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

public enum PaymentChannel
{
    Cash,
    GCash,
    BusinessGCash,
    CIMB,
    BPI,
    Maya,
    Other
}

/// <summary>Money received against one order. A transfer that settles several orders becomes one payment per order.</summary>
public class Payment : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public decimal Amount { get; set; }
    public PaymentChannel Channel { get; set; }
    public DateOnly PaidOn { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}
