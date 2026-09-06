using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

public class AddOn : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}
