using System.ComponentModel.DataAnnotations;
using Klowee.Api.Entities;

namespace Klowee.Api.Contracts.Menu;

public record MenuItemDto(
    Guid Id,
    string Name,
    string Description,
    Guid CategoryId,
    string CategoryName,
    string? PhotoUrl,
    bool IsArchived,
    LoyaltyMode LoyaltyMode,
    Guid? LoyaltyClubId);

public record MenuItemRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; init; }

    [Required]
    public Guid CategoryId { get; init; }

    [MaxLength(2048)]
    [Url]
    public string? PhotoUrl { get; init; }

    /// <summary>Inherit (the category's club, the default), None, or Club with LoyaltyClubId.</summary>
    public LoyaltyMode LoyaltyMode { get; init; } = LoyaltyMode.Inherit;

    /// <summary>Required when LoyaltyMode is Club; ignored otherwise.</summary>
    public Guid? LoyaltyClubId { get; init; }
}
