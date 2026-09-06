using System.ComponentModel.DataAnnotations;

namespace Klowee.Api.Contracts.Menu;

public record MenuItemDto(
    Guid Id,
    string Name,
    string Description,
    Guid CategoryId,
    string CategoryName,
    string? PhotoUrl,
    bool IsArchived);

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
}
