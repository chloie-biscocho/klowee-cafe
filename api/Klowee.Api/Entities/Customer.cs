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

    /// <summary>
    /// The private "my tab" link's secret (handover 17, decision 021): 32 random
    /// bytes, base64url. Whoever holds it sees this customer's tab. Null: no link.
    /// Replacing or clearing it kills the old link at once. Never logged.
    /// </summary>
    public string? TabToken { get; set; }

    public DateTimeOffset? TabTokenCreatedAt { get; set; }
}
