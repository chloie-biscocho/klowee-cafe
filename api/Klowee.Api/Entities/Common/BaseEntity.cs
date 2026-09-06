namespace Klowee.Api.Entities.Common;

/// <summary>
/// Base type for every persisted entity. Carries the identity, audit, and
/// soft-delete columns that every table in the schema shares. See decision
/// record docs/decisions/003-soft-delete-and-audit-fields.md.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>User id that created the row. Nullable until auth exists.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>When set, the row is soft-deleted and hidden by the global query filter.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
