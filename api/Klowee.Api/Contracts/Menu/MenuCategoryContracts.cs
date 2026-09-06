using System.ComponentModel.DataAnnotations;

namespace Klowee.Api.Contracts.Menu;

public record MenuCategoryDto(Guid Id, string Name, int SortOrder);

public record MenuCategoryRequest
{
    [Required]
    [MaxLength(80)]
    public string Name { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int SortOrder { get; init; }
}
