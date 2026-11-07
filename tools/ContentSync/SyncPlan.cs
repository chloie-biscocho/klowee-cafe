namespace Klowee.ContentSync;

/// <summary>What will happen to one table.</summary>
public sealed record TablePlan(
    string Table,
    long SourceLive,
    long SourceSoftDeleted,
    int Orphans,
    long DestinationNow,
    TableRows ToInsert);

/// <summary>
/// Turns the source's live rows into exactly what will be inserted:
/// children of rows that are not being copied are dropped (they would break a
/// foreign key), <c>created_by</c> is cleared (source user ids mean nothing on
/// the other side), and URLs into the source bucket are rewritten to the
/// destination bucket. Pure: no I/O, so it can be tested.
/// </summary>
public static class SyncPlanner
{
    /// <summary>The columns that may hold a media URL, per table.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> UrlColumns = new Dictionary<string, string[]>
    {
        ["menu_items"] = ["photo_url"],
        ["events"] = ["cover_photo_url"],
        ["event_photos"] = ["url"]
    };

    /// <summary>The site settings whose value is a media URL.</summary>
    public static readonly IReadOnlySet<string> UrlSettings = new HashSet<string> { "hero_image_url", "story_image_url" };

    /// <param name="sourceRows">Live source rows per table, for every table in scope.</param>
    /// <param name="destinationIds">
    /// Ids already at the destination for parent tables that are NOT in scope
    /// (with --tables): their rows stay, so children may point at them.
    /// </param>
    /// <param name="objectPaths">Receives every source-bucket object path the copied rows refer to.</param>
    public static Dictionary<string, (TableRows Rows, int Orphans)> Transform(
        IReadOnlyList<ContentTable> insertOrder,
        IReadOnlyDictionary<string, TableRows> sourceRows,
        IReadOnlyDictionary<string, HashSet<Guid>> destinationIds,
        StorageUrls source,
        StorageUrls destination,
        ISet<string> objectPaths)
    {
        var keptIds = new Dictionary<string, HashSet<Guid>>();
        var result = new Dictionary<string, (TableRows, int)>();

        foreach (var table in insertOrder)
        {
            var rows = sourceRows[table.Name];
            var idIndex = rows.IndexOf("id");
            var createdByIndex = rows.IndexOf("created_by");

            var parentChecks = table.Parents
                .Select(parent => (
                    Index: rows.IndexOf(parent.Column),
                    Allowed: keptIds.TryGetValue(parent.ParentTable, out var ids) ? ids
                        : destinationIds.TryGetValue(parent.ParentTable, out var existing) ? existing
                        : []))
                .ToList();

            var urlIndexes = UrlColumns.TryGetValue(table.Name, out var urlColumns)
                ? urlColumns.Select(rows.IndexOf).Where(i => i >= 0).ToArray()
                : [];
            var keyIndex = table.Name == "site_settings" ? rows.IndexOf("key") : -1;
            var valueIndex = table.Name == "site_settings" ? rows.IndexOf("value") : -1;

            var kept = new List<object?[]>();
            var orphans = 0;
            foreach (var original in rows.Rows)
            {
                if (parentChecks.Any(check => original[check.Index] is not Guid parentId || !check.Allowed.Contains(parentId)))
                {
                    orphans++;
                    continue;
                }

                var row = (object?[])original.Clone();
                if (createdByIndex >= 0)
                {
                    row[createdByIndex] = null;
                }

                foreach (var index in urlIndexes)
                {
                    row[index] = RewriteAndCollect(row[index] as string, source, destination, objectPaths);
                }

                if (keyIndex >= 0 && valueIndex >= 0 && row[keyIndex] is string key && UrlSettings.Contains(key))
                {
                    row[valueIndex] = RewriteAndCollect(row[valueIndex] as string, source, destination, objectPaths);
                }

                kept.Add(row);
            }

            keptIds[table.Name] = kept.Select(row => (Guid)row[idIndex]!).ToHashSet();
            result[table.Name] = (rows with { Rows = kept }, orphans);
        }

        return result;
    }

    private static string? RewriteAndCollect(string? url, StorageUrls source, StorageUrls destination, ISet<string> objectPaths)
    {
        var path = source.ObjectPath(url);
        if (path is not null)
        {
            objectPaths.Add(path);
        }

        return StorageUrls.Rewrite(url, source, destination);
    }
}
