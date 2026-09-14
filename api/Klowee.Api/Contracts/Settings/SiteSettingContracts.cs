using System.ComponentModel.DataAnnotations;

namespace Klowee.Api.Contracts.Settings;

public record SiteSettingDto(string Key, string Value);

public record SiteSettingRequest
{
    [Required]
    [MaxLength(120)]
    public string Key { get; init; } = string.Empty;

    /// <summary>May be empty — clearing a setting is a legitimate edit.</summary>
    [MaxLength(4000)]
    public string? Value { get; init; }
}
