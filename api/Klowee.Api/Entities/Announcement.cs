using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

public class Announcement : BaseEntity
{
    public string Text { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public DateTimeOffset ActiveFrom { get; set; }
    public DateTimeOffset? ActiveUntil { get; set; }
    public bool IsActive { get; set; } = true;
}
