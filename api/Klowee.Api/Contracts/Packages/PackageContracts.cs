using System.ComponentModel.DataAnnotations;

namespace Klowee.Api.Contracts.Packages;

public record PackageInclusionDto(Guid Id, string Text, int SortOrder);

public record PackageDto(
    Guid Id,
    string Name,
    decimal Price,
    string Description,
    string GuestCountNote,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<PackageInclusionDto> Inclusions);

public record PackageInclusionRequest
{
    [Required]
    [MaxLength(300)]
    public string Text { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int SortOrder { get; init; }
}

/// <summary>
/// The whole package, inclusions included. A PUT replaces the inclusion list in
/// full, the same way a menu version's items are replaced.
/// </summary>
public record PackageRequest
{
    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    [Range(0, 1000000)]
    public decimal Price { get; init; }

    [MaxLength(2000)]
    public string? Description { get; init; }

    [MaxLength(120)]
    public string? GuestCountNote { get; init; }

    public bool IsActive { get; init; } = true;

    [Range(0, int.MaxValue)]
    public int SortOrder { get; init; }

    public List<PackageInclusionRequest> Inclusions { get; init; } = [];
}
