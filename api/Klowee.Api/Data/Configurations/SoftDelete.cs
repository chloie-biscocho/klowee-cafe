namespace Klowee.Api.Data.Configurations;

/// <summary>
/// Shared SQL for the partial unique indexes. Uniqueness has to apply to live
/// rows only: with soft delete, a name that was "deleted" must be reusable, and
/// the same row must not block a new one forever. See
/// docs/decisions/003-soft-delete-and-audit-fields.md.
/// </summary>
internal static class SoftDelete
{
    public const string Filter = "deleted_at IS NULL";
}
