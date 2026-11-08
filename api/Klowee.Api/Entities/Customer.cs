using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

/// <summary>
/// Someone who buys drinks at the office, and whose tab and loyalty stamps are
/// tracked. Name is unique among live customers. See
/// docs/decisions/016-orders-and-payments-model.md.
/// </summary>
public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Nickname { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
