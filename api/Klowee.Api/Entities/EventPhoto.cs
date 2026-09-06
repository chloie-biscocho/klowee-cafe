using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

public class EventPhoto : BaseEntity
{
    public Guid EventId { get; set; }
    public Event? Event { get; set; }

    public string Url { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
}
