using System.ComponentModel.DataAnnotations;
using Klowee.Api.Entities;

namespace Klowee.Api.Contracts.Tabs;

// ---- Admin side (JWT) ----

/// <summary>A customer's private "my tab" link (decision 021).</summary>
public record TabLinkDto(string Url, DateTimeOffset CreatedAt);

public record PaymentMethodDto(
    Guid Id,
    PaymentChannel Channel,
    string Label,
    string AccountName,
    string? AccountNumber,
    string? QrUrl,
    bool IsActive,
    int SortOrder);

public record PaymentMethodRequest
{
    public PaymentChannel Channel { get; init; }

    [Required]
    [MaxLength(40)]
    public string Label { get; init; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string AccountName { get; init; } = string.Empty;

    [MaxLength(60)]
    public string? AccountNumber { get; init; }

    [MaxLength(2048)]
    [Url]
    public string? QrUrl { get; init; }

    public bool IsActive { get; init; } = true;

    [Range(0, int.MaxValue)]
    public int SortOrder { get; init; }
}

/// <param name="Outstanding">The customer's tab now, to judge the claim against.</param>
public record PaymentClaimDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    decimal Amount,
    PaymentChannel Channel,
    string? PaymentMethodLabel,
    string Reference,
    PaymentClaimStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    string? RejectReason,
    decimal Outstanding);

/// <summary>Confirms a claim: settles the payer's tab with its amount, reference and channel (the owner may override the channel or date).</summary>
public record ConfirmClaimRequest
{
    public DateOnly? PaidOn { get; init; }
    public PaymentChannel? Channel { get; init; }
}

public record RejectClaimRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}

/// <summary>A customer asking for a loyalty card from their tab page, waiting for the owners.</summary>
public record CardRequestDto(Guid MembershipId, Guid CustomerId, string CustomerName, Guid ClubId, string ClubName, DateTimeOffset RequestedAt);

// ---- Public side (the token is the key; no internal ids but claims', clubs' and payment methods') ----

public record PublicOrderDto(DateOnly OrderedOn, string Items, decimal Total, decimal Paid, decimal Due);

public record PublicPaymentDto(DateOnly PaidOn, PaymentChannel Channel, decimal Amount);

public record PublicClaimDto(Guid Id, decimal Amount, string Reference, PaymentClaimStatus Status, DateOnly CreatedOn, PaymentChannel Channel, string? RejectReason);

public record PublicRewardDto(int Slot, string Label, LoyaltyRewardType Type, string State, int Cycle);

/// <summary>A slot as the customer sees it: like the admin's, without the order behind it.</summary>
public record PublicSlotDto(int Slot, bool Filled, DateOnly? Date, string? Source);

public record PublicCardDto(int CardNumber, bool IsCurrent, IReadOnlyList<PublicSlotDto> Slots, IReadOnlyList<PublicRewardDto> Rewards);

/// <param name="Membership">"member", "requested" or "none"; stamps and rewards only for members.</param>
/// <param name="RequestedOn">When they asked for the card, for "Asked on Nov 13".</param>
/// <param name="ComesWithClubId">The club whose card brings this one (no Ask button of its own).</param>
public record PublicClubDto(
    Guid ClubId,
    string Name,
    LoyaltyClubStyle Style,
    int SlotCount,
    string Membership,
    DateOnly? RequestedOn,
    Guid? ComesWithClubId,
    string? ComesWithName,
    int Position,
    int Cycle,
    IReadOnlyList<PublicRewardDto> Rewards,
    int PendingDrinks,
    IReadOnlyList<PublicCardDto> Cards);

public record PublicPayMethodDto(Guid Id, PaymentChannel Channel, string Label, string AccountName, string? AccountNumber, string? QrUrl);

public record PublicPayDto(string? Note, IReadOnlyList<PublicPayMethodDto> Methods);

/// <param name="Name">Their nickname if set, else their name.</param>
/// <param name="Balance">What their tab owes: orders they pay for, less payments.</param>
public record PublicTabDto(
    string Name,
    decimal Balance,
    decimal PendingClaimsTotal,
    IReadOnlyList<PublicOrderDto> UnpaidOrders,
    IReadOnlyList<PublicPaymentDto> RecentPayments,
    IReadOnlyList<PublicClaimDto> Claims,
    IReadOnlyList<PublicClubDto> Clubs,
    PublicPayDto Pay);

public record PublicClaimRequest
{
    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Amount { get; init; }

    /// <summary>6 to 32 letters and digits: the transfer's reference number.</summary>
    [Required]
    public string Reference { get; init; } = string.Empty;

    [Required]
    public Guid PaymentMethodId { get; init; }
}
