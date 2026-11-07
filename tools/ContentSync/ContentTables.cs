namespace Klowee.ContentSync;

/// <summary>A foreign key from a content table to another content table.</summary>
public record ParentKey(string Column, string ParentTable);

/// <summary>One table the tool copies, with the foreign keys it depends on.</summary>
public record ContentTable(string Name, IReadOnlyList<ParentKey> Parents);

/// <summary>
/// The content tables, in dependency order: every table comes after the tables
/// it references, so inserting in this order never points at a row that is not
/// there yet, and deleting in reverse never removes a row something still
/// points at. <c>users</c> and <c>__EFMigrationsHistory</c> are deliberately
/// absent: the tool cannot name them. See docs/decisions/014-content-sync.md.
/// </summary>
public static class ContentTables
{
    public static readonly IReadOnlyList<ContentTable> All =
    [
        new("menu_categories", []),
        new("menu_items", [new("category_id", "menu_categories")]),
        new("add_ons", []),
        new("menu_versions", []),
        new("menu_version_items", [new("menu_item_id", "menu_items"), new("menu_version_id", "menu_versions")]),
        new("packages", []),
        new("package_inclusions", [new("package_id", "packages")]),
        new("events", []),
        new("event_photos", [new("event_id", "events")]),
        new("announcements", []),
        new("site_settings", [])
    ];

    /// <summary>
    /// The tables to copy, in insert order. With no selection, all of them.
    /// A selected table brings its dependents along: replacing menu_items
    /// alone would leave menu_version_items pointing at rows that were
    /// deleted (or, for cascading keys, delete them silently), so the rows
    /// that hang off a replaced table are replaced with it.
    /// </summary>
    public static IReadOnlyList<ContentTable> InsertOrder(IReadOnlyCollection<string>? selected = null)
    {
        if (selected is null || selected.Count == 0)
        {
            return All;
        }

        var unknown = selected.Where(name => All.All(table => table.Name != name)).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"Unknown table(s): {string.Join(", ", unknown)}. " +
                $"Content tables are: {string.Join(", ", All.Select(t => t.Name))}.");
        }

        var included = new HashSet<string>(selected);
        // All is in dependency order, so one pass picks up dependents of
        // dependents too.
        foreach (var table in All)
        {
            if (table.Parents.Any(parent => included.Contains(parent.ParentTable)))
            {
                included.Add(table.Name);
            }
        }

        return All.Where(table => included.Contains(table.Name)).ToList();
    }

    /// <summary>The same tables, children first: the order rows can be deleted in.</summary>
    public static IReadOnlyList<ContentTable> DeleteOrder(IReadOnlyCollection<string>? selected = null) =>
        InsertOrder(selected).Reverse().ToList();
}
