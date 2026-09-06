using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

public class PackageInclusion : BaseEntity
{
    public Guid PackageId { get; set; }
    public Package? Package { get; set; }

    public string Text { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
