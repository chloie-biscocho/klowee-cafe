using System.Diagnostics;
using Klowee.ContentSync;

// Copies the Klowee content tables and their media from one environment to
// another. Dry run by default; --apply asks for the destination project ref
// before it changes anything. docs/decisions/014-content-sync.md.

var apply = args.Contains("--apply");
string[]? selected = null;
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--tables" && i + 1 < args.Length)
    {
        selected = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
    else if (args[i] is "--help" or "-h")
    {
        Console.WriteLine("""
            Usage: dotnet run --project tools/ContentSync -- [--apply] [--tables a,b,...]

              (no flags)   dry run: compare source and destination, write nothing
              --apply      replace the destination's content with the source's
              --tables     limit to these tables (their dependent tables come along)

            Reads SYNC_SOURCE_DB, SYNC_SOURCE_SUPABASE_URL, SYNC_SOURCE_SERVICE_KEY,
                  SYNC_DEST_DB,   SYNC_DEST_SUPABASE_URL,   SYNC_DEST_SERVICE_KEY.
            """);
        return 0;
    }
    else if (args[i] is not ("--apply" or "--tables"))
    {
        Console.Error.WriteLine($"Unknown argument: {args[i]}. Try --help.");
        return 2;
    }
}

var clock = Stopwatch.StartNew();
var cancellation = CancellationToken.None;

try
{
    var (source, destination) = SyncSettings.Load(Environment.GetEnvironmentVariable);
    var insertOrder = ContentTables.InsertOrder(selected);

    Console.WriteLine($"Source       {source.Describe()}");
    Console.WriteLine($"Destination  {destination.Describe()}");
    Console.WriteLine($"Tables       {string.Join(", ", insertOrder.Select(t => t.Name))}");
    Console.WriteLine($"Mode         {(apply ? "APPLY" : "dry run (nothing is written)")}");
    Console.WriteLine();

    await using var sourceDb = await ContentDatabase.OpenAsync(source, readOnly: true, cancellation);
    await using var destinationDb = await ContentDatabase.OpenAsync(destination, readOnly: !apply, cancellation);

    // ---- Read and plan ----
    var sourceRows = new Dictionary<string, TableRows>();
    var softDeleted = new Dictionary<string, long>();
    var destinationCounts = new Dictionary<string, long>();
    foreach (var table in insertOrder)
    {
        sourceRows[table.Name] = await ContentDatabase.ReadLiveRowsAsync(sourceDb, table.Name, cancellation);
        softDeleted[table.Name] = await ContentDatabase.CountSoftDeletedAsync(sourceDb, table.Name, cancellation);
        destinationCounts[table.Name] = await ContentDatabase.CountAllAsync(destinationDb, table.Name, cancellation);

        var sourceColumns = sourceRows[table.Name].Columns.Order(StringComparer.Ordinal).ToList();
        var destinationColumns = await ContentDatabase.ColumnsAsync(destinationDb, table.Name, cancellation);
        if (!sourceColumns.SequenceEqual(destinationColumns.Order(StringComparer.Ordinal)))
        {
            throw new SettingsException(
                $"{table.Name} has different columns at source and destination. Apply the same migrations to both first.");
        }
    }

    // Parents outside --tables stay at the destination; children may point at them.
    var inScope = insertOrder.Select(t => t.Name).ToHashSet();
    var destinationIds = new Dictionary<string, HashSet<Guid>>();
    foreach (var parent in insertOrder.SelectMany(t => t.Parents).Select(p => p.ParentTable).Distinct().Where(p => !inScope.Contains(p)))
    {
        destinationIds[parent] = await ContentDatabase.IdsAsync(destinationDb, parent, cancellation);
    }

    var objectPaths = new SortedSet<string>(StringComparer.Ordinal);
    var planned = SyncPlanner.Transform(insertOrder, sourceRows, destinationIds, source.Storage, destination.Storage, objectPaths);
    var plans = insertOrder.Select(t => new TablePlan(
        t.Name, sourceRows[t.Name].Rows.Count, softDeleted[t.Name], planned[t.Name].Orphans,
        destinationCounts[t.Name], planned[t.Name].Rows)).ToList();

    // ---- Storage: what is missing, or different, at the destination ----
    using var sourceStorage = new StorageClient(source);
    using var destinationStorage = new StorageClient(destination);
    var toCopy = new List<string>();
    var alreadyThere = 0;
    foreach (var path in objectPaths)
    {
        var sourceSize = await sourceStorage.SizeAsync(path, cancellation)
            ?? throw new StorageCopyException($"{path} is referenced by a row but missing from the source bucket.");
        var destinationSize = await destinationStorage.SizeAsync(path, cancellation);
        if (destinationSize == sourceSize)
        {
            alreadyThere++;
        }
        else
        {
            toCopy.Add(path);
        }
    }

    PrintPlan(plans, objectPaths.Count, alreadyThere, toCopy.Count);

    if (!apply)
    {
        Console.WriteLine();
        Console.WriteLine($"Dry run: nothing was written. {clock.Elapsed.TotalSeconds:0.0}s.");
        return 0;
    }

    // ---- Confirm ----
    Console.WriteLine();
    Console.WriteLine($"This DELETES every row of the tables above in project {destination.ProjectRef} and replaces them.");
    Console.Write($"Type the destination project ref ({destination.ProjectRef}) to continue: ");
    var typed = Console.ReadLine();
    if (typed?.Trim() != destination.ProjectRef)
    {
        Console.WriteLine("Did not match. Nothing was changed.");
        return 1;
    }

    // ---- Storage first: a failure here leaves the database untouched ----
    var copied = 0;
    long bytes = 0;
    foreach (var path in toCopy)
    {
        var (content, contentType) = await sourceStorage.DownloadAsync(path, cancellation);
        await destinationStorage.UploadAsync(path, content, contentType, cancellation);
        copied++;
        bytes += content.Length;
        Console.WriteLine($"  storage  copied   {path} ({content.Length / 1024.0:0.0} kB)");
    }

    // ---- Database: delete and insert in one transaction ----
    await using var transaction = await destinationDb.BeginTransactionAsync(cancellation);
    var deleted = new Dictionary<string, int>();
    foreach (var table in insertOrder.Reverse())
    {
        deleted[table.Name] = await ContentDatabase.DeleteAllAsync(destinationDb, transaction, table.Name, cancellation);
        Console.WriteLine($"  table    deleted  {table.Name,-20} {deleted[table.Name],5} rows");
    }

    var inserted = new Dictionary<string, int>();
    foreach (var plan in plans)
    {
        inserted[plan.Table] = await ContentDatabase.InsertAsync(destinationDb, transaction, plan.Table, plan.ToInsert, cancellation);
        Console.WriteLine($"  table    inserted {plan.Table,-20} {inserted[plan.Table],5} rows");
    }

    await transaction.CommitAsync(cancellation);

    Console.WriteLine();
    Console.WriteLine("Summary");
    Console.WriteLine($"  rows deleted   {deleted.Values.Sum()}");
    Console.WriteLine($"  rows inserted  {inserted.Values.Sum()} across {inserted.Count} tables");
    Console.WriteLine($"  objects copied {copied} ({bytes / 1024.0 / 1024.0:0.00} MB), {alreadyThere} already at destination");
    Console.WriteLine($"  elapsed        {clock.Elapsed.TotalSeconds:0.0}s");
    return 0;
}
catch (Exception ex) when (ex is SettingsException or StorageCopyException or ArgumentException)
{
    Console.Error.WriteLine($"Stopped: {ex.Message}");
    Console.Error.WriteLine("Nothing was changed in the destination database.");
    return 1;
}
catch (Npgsql.NpgsqlException ex)
{
    // The transaction was never committed, so disposing it rolled everything back.
    // Npgsql's messages carry SQL state and table names, not credentials.
    Console.Error.WriteLine($"Stopped: database error {(ex as Npgsql.PostgresException)?.SqlState} {ex.Message}");
    Console.Error.WriteLine("The transaction was rolled back; the destination database is unchanged.");
    return 1;
}

static void PrintPlan(IReadOnlyList<TablePlan> plans, int objects, int alreadyThere, int toCopy)
{
    Console.WriteLine($"{"Table",-20} {"Source",7} {"Soft-del",8} {"Orphans",7} {"Dest now",8} {"Insert",7}");
    Console.WriteLine(new string('-', 62));
    foreach (var plan in plans)
    {
        Console.WriteLine(
            $"{plan.Table,-20} {plan.SourceLive,7} {plan.SourceSoftDeleted,8} {plan.Orphans,7} {plan.DestinationNow,8} {plan.ToInsert.Rows.Count,7}");
    }

    Console.WriteLine(new string('-', 62));
    Console.WriteLine(
        $"{"Total",-20} {plans.Sum(p => p.SourceLive),7} {plans.Sum(p => p.SourceSoftDeleted),8} {plans.Sum(p => p.Orphans),7} " +
        $"{plans.Sum(p => p.DestinationNow),8} {plans.Sum(p => p.ToInsert.Rows.Count),7}");
    Console.WriteLine();
    Console.WriteLine($"Storage: {objects} objects referenced, {alreadyThere} already at destination (same size), {toCopy} to copy.");
    Console.WriteLine("Source = live rows; Soft-del = skipped; Orphans = live rows whose parent is not copied (skipped);");
    Console.WriteLine("Dest now = all rows the destination holds (an apply deletes them); Insert = rows an apply writes.");
}
