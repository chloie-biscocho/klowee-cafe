using System.ComponentModel.DataAnnotations;

namespace Klowee.Api.Contracts.Announcements;

public record AnnouncementDto(
    Guid Id,
    string Text,
    string? LinkUrl,
    DateTimeOffset ActiveFrom,
    DateTimeOffset? ActiveUntil,
    bool IsActive);

public record AnnouncementRequest
{
    [Required]
    [MaxLength(500)]
    public string Text { get; init; } = string.Empty;

    [MaxLength(2048)]
    [Url]
    public string? LinkUrl { get; init; }

    [Required]
    public DateTimeOffset ActiveFrom { get; init; }

    /// <summary>Null means open-ended. When set, must be after <see cref="ActiveFrom"/>.</summary>
    public DateTimeOffset? ActiveUntil { get; init; }

    public bool IsActive { get; init; } = true;
}
