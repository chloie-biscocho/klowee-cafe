using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

public enum EventStatus
{
    Planned,
    Upcoming,
    Done,
    Cancelled
}

public class Event : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public string? Address { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public string? Description { get; set; }
    public string? CoverPhotoUrl { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Planned;
    public bool IsPublished { get; set; }

    public ICollection<EventPhoto> Photos { get; set; } = new List<EventPhoto>();
}
