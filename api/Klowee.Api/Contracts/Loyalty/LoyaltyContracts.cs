using System.ComponentModel.DataAnnotations;
using Klowee.Api.Entities;

namespace Klowee.Api.Contracts.Loyalty;

public record LoyaltyClubRewardDto(
    Guid Id,
    int Slot,
    LoyaltyRewardType Type,
    decimal? Percent,
    Guid? AddOnId,
    string? AddOnName,
    string Label);

/// <param name="CategoryIds">The menu categories whose drinks feed this club (items may override).</param>
/// <param name="AutoJoinFromClubId">Members of that club get this card automatically.</param>
/// <param name="MemberCount">Customers holding this card (Active memberships).</param>
public record LoyaltyClubDto(
    Guid Id,
    string Name,
    LoyaltyClubStyle Style,
    int SlotCount,
    bool IsActive,
    DateOnly? ActivatedOn,
    int SortOrder,
    Guid? AutoJoinFromClubId,
    IReadOnlyList<LoyaltyClubRewardDto> Rewards,
    IReadOnlyList<Guid> CategoryIds,
    int MemberCount,
    int? AccentLetter = null);

public record LoyaltyClubRewardRequest
{
    [Range(1, 100)]
    public int Slot { get; init; }

    public LoyaltyRewardType Type { get; init; }

    /// <summary>PercentOff only: 1–100.</summary>
    public decimal? Percent { get; init; }

    /// <summary>FreeAddOn only.</summary>
    public Guid? AddOnId { get; init; }

    [Required]
    [MaxLength(40)]
    public string Label { get; init; } = string.Empty;
}

/// <summary>
/// A whole club. The rewards array replaces the club's rewards in full, matched
/// by slot (a slot kept keeps its reward's identity, so its uses stay used).
/// CategoryIds, when sent, replaces which categories feed the club; null leaves them.
/// </summary>
public record LoyaltyClubRequest
{
    [Required]
    [MaxLength(80)]
    public string Name { get; init; } = string.Empty;

    public LoyaltyClubStyle Style { get; init; }

    [Range(1, 100)]
    public int SlotCount { get; init; } = 12;

    public bool IsActive { get; init; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; init; }

    /// <summary>Members of this other club get this card automatically. Not itself, and no loops.</summary>
    public Guid? AutoJoinFromClubId { get; init; }

    public List<LoyaltyClubRewardRequest> Rewards { get; init; } = [];

    public List<Guid>? CategoryIds { get; init; }

    /// <summary>The index in Name of the letter printed in pink, or null for none. Must point at a letter.</summary>
    public int? AccentLetter { get; init; }
}

// ---- Handover 17a: stamps given by mistake to carried-over cards ----

/// <summary>A stamp from an order dated on or before the card's carry-over day: already on the paper card.</summary>
public record StrayStampDto(Guid CustomerId, string CustomerName, Guid ClubId, string ClubName, Guid OrderId, DateOnly OrderedOn, int Stamps);

/// <param name="Before">"card 3, 7 of 12" now.</param>
/// <param name="After">The same card once the stray stamps are voided.</param>
public record StrayPositionDto(string CustomerName, string ClubName, DateOnly CarryOverDay, string Before, string After);

/// <summary>A reward an order already used that the card would not have earned without the stray stamps. Listed, never undone.</summary>
public record StrayRewardDto(string CustomerName, string ClubName, string Reward, int CardNumber, int Slot, Guid OrderId, DateOnly OrderedOn);

public record StrayStampsDto(IReadOnlyList<StrayStampDto> Stamps, IReadOnlyList<StrayPositionDto> Positions, IReadOnlyList<StrayRewardDto> UsedRewards);
