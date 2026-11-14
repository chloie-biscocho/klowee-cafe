using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

/// <summary>
/// An account the owners take transfers on (GCash, CIMB Bank…), shown to
/// customers on their tab page with its QR code (handover 17).
/// </summary>
public class PaymentMethod : BaseEntity
{
    public PaymentChannel Channel { get; set; }

    /// <summary>What the customer sees on the switch ("GCash", "CIMB Bank").</summary>
    public string Label { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;

    /// <summary>Shown with a Copy button when set.</summary>
    public string? AccountNumber { get; set; }

    /// <summary>The uploaded QR image (folder "site").</summary>
    public string? QrUrl { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public enum PaymentClaimStatus
{
    Pending,
    Confirmed,
    Rejected
}

/// <summary>
/// A customer saying "I've paid" from their tab page (decision 021). Nothing
/// moves until an owner confirms it: confirming runs the usual settle with
/// the claim's amount, reference and channel; rejecting records why.
/// </summary>
public class PaymentClaim : BaseEntity
{
    /// <summary>The payer: the customer whose tab the link shows.</summary>
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Copied from the chosen method when the claim is made.</summary>
    public PaymentChannel Channel { get; set; }

    public Guid? PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>The transfer's reference number, 6 to 32 letters and digits.</summary>
    public string Reference { get; set; } = string.Empty;

    public PaymentClaimStatus Status { get; set; } = PaymentClaimStatus.Pending;

    /// <summary>The owner (user id) who confirmed or rejected it.</summary>
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? RejectReason { get; set; }
    public string? Note { get; set; }
}
