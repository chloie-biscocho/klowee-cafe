using System.ComponentModel.DataAnnotations;

namespace Klowee.Api.Contracts.Menu;

/// <param name="LoyaltyClubId">The club this category's drinks stamp; null for none.</param>
public record MenuCategoryDto(Guid Id, string Name, int SortOrder, string? Description, Guid? LoyaltyClubId);

public record MenuCategoryRequest
{
    [Required]
    [MaxLength(80)]
    public string Name { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int SortOrder { get; init; }

    /// <summary>Optional; blank is stored as null.</summary>
    [MaxLength(200)]
    public string? Description { get; init; }

    /// <summary>The club this category's drinks stamp. Like every field here, a PUT replaces it: null means no club.</summary>
    public Guid? LoyaltyClubId { get; init; }
}
