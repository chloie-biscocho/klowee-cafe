using System.ComponentModel.DataAnnotations;

namespace Klowee.Api.Contracts.Menu;

public record AddOnDto(Guid Id, string Name, decimal Price, bool IsActive);

public record AddOnRequest
{
    [Required]
    [MaxLength(80)]
    public string Name { get; init; } = string.Empty;

    [Range(0, 100000)]
    public decimal Price { get; init; }

    public bool IsActive { get; init; } = true;
}
