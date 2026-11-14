using System.ComponentModel.DataAnnotations;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Entities;

namespace Klowee.Api.Contracts.Customers;

/// <param name="OutstandingBalance">What this customer's tab owes: orders they pay for, minus payments.</param>
public record CustomerSummaryDto(
    Guid Id,
    string Name,
    string? Nickname,
    string? Notes,
    bool IsActive,
    decimal OutstandingBalance,
    IReadOnlyList<CustomerClubDto> Clubs,
    DateOnly? LastOrderOn);

public record CustomerDetailDto(
    Guid Id,
    string Name,
    string? Nickname,
    string? Notes,
    bool IsActive,
    decimal OutstandingBalance,
    IReadOnlyList<CustomerClubDto> Clubs,
    DateOnly? LastOrderOn,
    IReadOnlyList<OrderSummaryDto> RecentOrders);

/// <summary>
/// One reward on the customer's card. The current card lists every reward
/// (locked, available or used); rewards from earlier cards appear only while
/// still available, since they never expire.
/// </summary>
/// <param name="State">"locked", "available" or "used".</param>
/// <param name="Cycle">Which pass round the card the reward belongs to (0-based).</param>
/// <param name="RedemptionId">Set when used; DELETE it to make the reward available again.</param>
/// <param name="OrderId">Set when an order used it.</param>
public record CustomerRewardDto(
    Guid RewardId,
    int Slot,
    string Label,
    LoyaltyRewardType Type,
    string State,
    int Cycle,
    Guid? RedemptionId,
    Guid? OrderId);

/// <summary>A customer's place on one active club's card, derived from the ledger (decision 020).</summary>
/// <param name="Membership">"member", "requested" or "none". Cards are by invitation (handover 16a); a non-member's position and rewards are empty.</param>
/// <param name="MembershipId">The membership row, for taking the card away; null for "none".</param>
/// <param name="JoinedOn">The first day orders stamp the card; null for "since the beginning" (carried-over cardholders) or no card.</param>
/// <param name="Position">Slots filled on the current card.</param>
/// <param name="Cycle">Completed cards (0-based index of the current one).</param>
/// <param name="PendingDrinks">Drinks of this club on the customer's not-yet-paid orders.</param>
public record CustomerClubDto(
    Guid ClubId,
    string Name,
    LoyaltyClubStyle Style,
    string Membership,
    Guid? MembershipId,
    DateOnly? JoinedOn,
    int SlotCount,
    int Position,
    int Cycle,
    IReadOnlyList<CustomerRewardDto> Rewards,
    int PendingDrinks,
    IReadOnlyList<LoyaltyCardDto> Cards);

/// <summary>
/// One slot of a card. A filled slot from a paid order has the order's date
/// and id; one from the paper-card carry-over ("carriedOver") or a hand
/// adjustment ("adjustment") has neither: dates are never invented.
/// </summary>
/// <param name="Source">"order", "carriedOver" or "adjustment"; null when empty.</param>
public record LoyaltySlotDto(int Slot, bool Filled, DateOnly? Date, Guid? OrderId, string? Source);

/// <summary>
/// One card in the stack (handover 16b): every card from 1 to the current
/// one, each with all its slots and all its rewards. Same data for the admin
/// and the customer page.
/// </summary>
public record LoyaltyCardDto(
    int CardNumber,
    bool IsCurrent,
    IReadOnlyList<LoyaltySlotDto> Slots,
    IReadOnlyList<CustomerRewardDto> Rewards);

public record CustomerRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(80)]
    public string? Nickname { get; init; }

    [MaxLength(1000)]
    public string? Notes { get; init; }

    public bool IsActive { get; init; } = true;
}

public record LoyaltyEntryDto(
    Guid Id,
    Guid ClubId,
    string ClubName,
    int Delta,
    LoyaltyReason Reason,
    Guid? OrderId,
    string? Notes,
    DateTimeOffset CreatedAt);

public record LoyaltyAdjustRequest
{
    [Required]
    public Guid ClubId { get; init; }

    [Range(-1000, 1000)]
    public int Delta { get; init; }

    [MaxLength(500)]
    public string? Notes { get; init; }
}

/// <summary>Marks a reward used by hand (no order): the owners' paper card says so.</summary>
public record LoyaltyRedemptionRequest
{
    [Required]
    public Guid ClubId { get; init; }

    [Required]
    public Guid RewardId { get; init; }

    [Range(0, 10000)]
    public int Cycle { get; init; }

    [MaxLength(500)]
    public string? Note { get; init; }
}

public record LoyaltyRedemptionDto(
    Guid Id,
    Guid ClubId,
    Guid RewardId,
    int Cycle,
    Guid? OrderId,
    string? Note,
    DateTimeOffset CreatedAt);

/// <summary>Gives a customer a card (handover 16a): Active from today, and the cards that come with it.</summary>
public record LoyaltyMembershipRequest
{
    [Required]
    public Guid ClubId { get; init; }

    [MaxLength(500)]
    public string? Note { get; init; }
}

public record LoyaltyMembershipDto(
    Guid Id,
    Guid ClubId,
    MembershipStatus Status,
    DateOnly? JoinedOn,
    string? Note);
