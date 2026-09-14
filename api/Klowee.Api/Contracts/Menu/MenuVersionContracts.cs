using System.ComponentModel.DataAnnotations;
using Klowee.Api.Entities;

namespace Klowee.Api.Contracts.Menu;

/// <summary>List row for <c>GET /api/menu/versions</c>.</summary>
public record MenuVersionSummaryDto(
    Guid Id,
    string Name,
    MenuContext Context,
    DateOnly EffectiveFrom,
    bool IsPublished,
    string? Notes,
    int ItemCount);

/// <summary>One priced line on a version, flattened for the client.</summary>
public record MenuVersionItemDto(
    Guid Id,
    Guid MenuItemId,
    string Name,
    Guid CategoryId,
    string CategoryName,
    decimal Price,
    bool IsAvailable,
    bool IsFeatured,
    int SortOrder);

/// <summary>A version plus its items, ordered by category sort then item sort.</summary>
public record MenuVersionDetailDto(
    Guid Id,
    string Name,
    MenuContext Context,
    DateOnly EffectiveFrom,
    bool IsPublished,
    string? Notes,
    IReadOnlyList<MenuVersionItemDto> Items);

public record CreateMenuVersionRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [EnumDataType(typeof(MenuContext))]
    public MenuContext Context { get; init; }

    [Required]
    public DateOnly EffectiveFrom { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }

    /// <summary>When set, every version item of that version is duplicated into the new one.</summary>
    public Guid? CopyFromVersionId { get; init; }
}

/// <summary>Context is immutable after creation, so it is absent here.</summary>
public record UpdateMenuVersionRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public DateOnly EffectiveFrom { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }
}

/// <summary>One entry of the full-replacement payload for <c>PUT /versions/{id}/items</c>.</summary>
public record MenuVersionItemRequest
{
    [Required]
    public Guid MenuItemId { get; init; }

    [Range(0, 100000)]
    public decimal Price { get; init; }

    public bool IsAvailable { get; init; } = true;

    public bool IsFeatured { get; init; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; init; }
}
