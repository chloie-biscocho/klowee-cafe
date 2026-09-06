using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

public class MenuCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}
