using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

public class Package : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Free-form guest note, e.g. "good for 50 pax".</summary>
    public string GuestCountNote { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<PackageInclusion> Inclusions { get; set; } = new List<PackageInclusion>();
}
