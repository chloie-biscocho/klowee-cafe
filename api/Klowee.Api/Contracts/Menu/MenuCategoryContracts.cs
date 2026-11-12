using System.ComponentModel.DataAnnotations;

namespace Klowee.Api.Contracts.Menu;

public record MenuCategoryDto(Guid Id, string Name, int SortOrder, string? Description);

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
}
