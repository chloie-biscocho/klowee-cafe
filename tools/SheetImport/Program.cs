using System.Globalization;
using System.Text;
using Klowee.SheetImport;

// Imports the owners' sales workbook (May 2025 – Nov 2026) through the admin
// API. Handover 13. Run from the repository root.
//
//   parse                 Stages 1–2: read the office tabs, write parsed.csv and summary.txt,
//                         and add new keys to customers.csv / drinks.csv (owner values are kept).
//   (no command)          Stages 3–7 as a dry run: print the plan and the planned reconciliation.
//   --apply               Write it: retired drinks, historical versions, customers, orders,
//                         payments; then reconcile against the API. Re-runs skip imported rows.
//   --rollback [--apply]  Delete every order carrying an import marker.
//
// Never prints credentials. Writes only through the API.

const string Dir = "docs/import";
var apply = args.Contains("--apply");
var rollback = args.Contains("--rollback");
var workbookPath = args.SkipWhile(a => a != "--workbook").Skip(1).FirstOrDefault() ?? $"{Dir}/KLOWEE_CAFE_SALES_REPORT.xlsx";

var missing = new[] { "KLOWEE_API_URL", "KLOWEE_OWNER_EMAIL", "KLOWEE_OWNER_PASSWORD" }
    .Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))).ToList();
if (missing.Count > 0)
{
    Console.Error.WriteLine($"Missing environment variable(s): {string.Join(", ", missing)}.");
    return 1;
}

try
{
    var (sheetRows, tabs) = Workbook.Read(workbookPath);
    var rows = sheetRows.Select(Rows.Parse).ToList();
    var apiUrl = Environment.GetEnvironmentVariable("KLOWEE_API_URL")!;
    using var api = await Api.SignInAsync(apiUrl, Environment.GetEnvironmentVariable("KLOWEE_OWNER_EMAIL")!,
        Environment.GetEnvironmentVariable("KLOWEE_OWNER_PASSWORD")!);

    if (args.FirstOrDefault() == "parse")
    {
        return await ParseAsync(api, rows, tabs, workbookPath, new Uri(apiUrl).Host);
    }

    Console.WriteLine($"API   {new Uri(apiUrl).Host}");
    Console.WriteLine($"Mode  {(rollback ? "ROLLBACK " : "IMPORT ")}{(apply ? "APPLY" : "dry run (nothing is written)")}");
    Console.WriteLine();

    var importer = new Importer(api, Console.Out);
    var first = rows.Min(r => r.Date);
    var last = rows.Max(r => r.Date);
    var existing = await importer.ExistingOrdersAsync(new DateOnly(first.Year, first.Month, 1), new DateOnly(last.Year, last.Month, 1).AddMonths(1).AddDays(-1));
    var imported = existing.Where(e => e.Marker is not null).ToList();

    if (rollback)
    {
        Console.WriteLine($"Imported orders found: {imported.Count}, ₱{imported.Sum(o => o.Total):#,0.##}. Other orders in the range are not touched: {existing.Count - imported.Count}.");
        if (apply && imported.Count > 0) await importer.RollbackAsync(imported);
        return 0;
    }

    var settings = await api.SettingsAsync();
    var perReward = int.TryParse(settings.FirstOrDefault(s => s.Key == "loyalty_stamps_per_reward")?.Value, out var n) ? n : 10;
    var addOns = await api.AddOnsAsync();
    decimal Price(string name) => addOns.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Price
        ?? throw new ImportException($"Add-on \"{name}\" not found.");

    var plan = Planner.Build(rows, MappingFiles.Customers($"{Dir}/customers.csv"), MappingFiles.Drinks($"{Dir}/drinks.csv"),
        Price("Extra shot"), Price("Oat milk"), perReward);

    var items = await api.ItemsAsync();
    var versions = await api.VersionsAsync();
    var people = await importer.CustomerIdsAsync();
    var report = PlanText(plan, items, versions, people, imported, perReward, Price("Extra shot"), Price("Oat milk"));
    File.WriteAllText($"{Dir}/plan.txt", report, new UTF8Encoding(false));
    Console.Write(report.Split("\nORDERS (")[0]);
    Console.WriteLine($"(the full plan, every order and every skipped row, is in {Dir}/plan.txt)");
    Console.WriteLine();

    if (!apply)
    {
        var table = Reconcile.Table(tabs, rows, plan, null, null);
        File.AppendAllText($"{Dir}/plan.txt", "\n" + table);
        Console.Write(table);
        return 0;
    }

    await importer.ApplyAsync(plan, existing);

    // ---- Stage 7 against what the API now holds ----
    var after = await importer.ExistingOrdersAsync(new DateOnly(first.Year, first.Month, 1), new DateOnly(last.Year, last.Month, 1).AddMonths(1).AddDays(-1));
    var tabOf = plan.Orders.ToDictionary(o => o.Marker, o => o.Row.Tab);
    var byTab = after.Where(e => e.Marker is not null && tabOf.ContainsKey(e.Marker))
        .GroupBy(e => tabOf[e.Marker!]).ToDictionary(g => g.Key, g => g.Sum(e => e.Total));
    // Only the import's customers: anyone else's tab isn't the sheet's to explain.
    var outstanding = (await api.CustomersAsync()).Where(c => c.OutstandingBalance != 0 && plan.Customers.Contains(c.Name, StringComparer.OrdinalIgnoreCase))
        .ToDictionary(c => c.Name, c => c.OutstandingBalance, StringComparer.OrdinalIgnoreCase);
    var final = Reconcile.Table(tabs, rows, plan, byTab, outstanding);
    File.WriteAllText($"{Dir}/reconcile.txt", final, new UTF8Encoding(false));
    Console.WriteLine();
    Console.Write(final);
    var others = after.Where(e => e.Marker is null).ToList();
    if (others.Count > 0)
        Console.WriteLine($"  Not imported, so not in the tables: {others.Count} order(s) entered in the admin in this range, ₱{others.Sum(o => o.Total):#,0.##}.");
    return 0;
}
catch (ImportException ex)
{
    Console.Error.WriteLine($"Stopped: {ex.Message}");
    return 1;
}

static async Task<int> ParseAsync(Api api, List<ParsedRow> rows, List<TabResult> tabs, string workbook, string host)
{
    var items = await api.ItemsAsync();
    Output.WriteParsed("docs/import/parsed.csv", rows);
    var importable = rows.Where(r => r.Skip.Length == 0).ToList();
    var customers = Mappings.WriteCustomers("docs/import/customers.csv", importable);
    var drinks = Mappings.WriteDrinks("docs/import/drinks.csv", importable, items);
    var summary = Output.Summary(workbook, tabs, rows, customers, drinks, host, items.Count);
    File.WriteAllText("docs/import/summary.txt", summary, new UTF8Encoding(false));
    Console.Write(summary);
    return 0;
}

static string PlanText(ImportPlan plan, List<Item> items, List<VersionSummary> versions, Dictionary<string, Guid> people,
    List<ExistingOrder> imported, int perReward, decimal shot, decimal oat)
{
    var s = new StringBuilder();
    static string M(decimal d) => d.ToString("#,0.##", CultureInfo.InvariantCulture);
    var done = imported.Select(i => i.Marker!).ToHashSet();
    var toCreate = plan.Orders.Where(o => !done.Contains(o.Marker)).ToList();

    s.AppendLine($"Replay settings: {perReward} stamps per reward; add-ons at today's prices (extra shot ₱{M(shot)}, oat milk ₱{M(oat)}).");
    s.AppendLine();
    var newItems = plan.Items.Where(i => !items.Any(x => x.Name.Equals(i, StringComparison.OrdinalIgnoreCase))).ToList();
    s.AppendLine($"RETIRED DRINKS TO CREATE ({newItems.Count}, in category \"{Importer.RetiredCategory}\", archived after the versions are built)");
    foreach (var i in newItems) s.AppendLine($"  {i}");
    var archived = plan.Items.Where(i => items.Any(x => x.IsArchived && x.Name.Equals(i, StringComparison.OrdinalIgnoreCase))).ToList();
    s.AppendLine($"  Already archived, unarchived for a moment while versions are priced, then archived again: {string.Join(", ", archived)}");

    s.AppendLine();
    s.AppendLine($"HISTORICAL OFFICE VERSIONS ({plan.Versions.Count}, unpublished; the current office and pop-up menus are not touched)");
    foreach (var v in plan.Versions)
    {
        var state = versions.Any(x => x.Context == "Office" && x.Name == v.Name) ? "exists" : "create";
        s.AppendLine($"  {state,-7} {v.Name}: {v.Prices.Count} drinks. Changes: {string.Join("; ", v.Changes)}");
    }

    var newPeople = plan.Customers.Where(c => !people.ContainsKey(c)).ToList();
    s.AppendLine();
    s.AppendLine($"CUSTOMERS ({plan.Customers.Count}: {newPeople.Count} to create, {plan.Customers.Count - newPeople.Count} already there)");
    s.AppendLine($"  create: {string.Join(", ", newPeople)}");

    s.AppendLine();
    s.AppendLine($"ORDERS: {plan.Orders.Count} planned, {plan.Orders.Count - toCreate.Count} already imported, {toCreate.Count} to create, ₱{M(toCreate.Sum(o => o.Total))}");
    s.AppendLine($"  payments: {toCreate.Count(o => o.Pay)}, ₱{M(toCreate.Where(o => o.Pay).Sum(o => o.Total))}, by channel {string.Join(", ", toCreate.Where(o => o.Pay).GroupBy(o => o.Channel).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}"))}");
    s.AppendLine($"  left open (unpaid, on tabs): {toCreate.Count(o => !o.Pay && o.Total > 0)}, ₱{M(toCreate.Where(o => !o.Pay).Sum(o => o.Total))}");
    s.AppendLine($"  discounts: {string.Join(", ", toCreate.GroupBy(o => o.DiscountType).Select(g => $"{g.Key} {g.Count()}"))}");
    s.AppendLine($"  with a payer (c/o): {toCreate.Count(o => o.Payer is not null)}");
    s.AppendLine($"  total equals the sheet's: {toCreate.Count(o => o.Shortfall == 0)}; short of it: {toCreate.Count(o => o.Shortfall > 0)}, ₱{M(toCreate.Sum(o => o.Shortfall))} in all");

    s.AppendLine();
    s.AppendLine($"DECISIONS WORTH A LOOK ({plan.Orders.Count(o => o.Decisions.Count > 0)} orders)");
    foreach (var group in plan.Orders.SelectMany(o => o.Decisions.Select(d => (o, d))).GroupBy(x => Kind(x.d)).OrderByDescending(g => g.Count()))
    {
        s.AppendLine($"  {group.Count(),3}  {group.Key}");
        foreach (var (o, d) in group) s.AppendLine($"         {o.Row.Tab + ":" + o.Row.Row,-18} {o.Customer} | {o.Item}: {d}");
    }

    s.AppendLine();
    s.AppendLine($"SKIPPED ({plan.Skipped.Count} rows, ₱{M(plan.Skipped.Sum(k => k.Row.Total))})");
    foreach (var group in plan.Skipped.GroupBy(k => Kind(k.Reason)).OrderByDescending(g => g.Count()))
    {
        s.AppendLine($"  {group.Count(),3}  {group.Key}");
        foreach (var k in group) s.AppendLine($"         {k.Row.Tab + ":" + k.Row.Row,-18} {k.Row.RawName} | {k.Row.RawDrink} | ₱{M(k.Row.Total)} | {k.Reason}");
    }

    s.AppendLine();
    s.AppendLine("ORDERS (date order; * = already imported)");
    foreach (var o in plan.Orders)
    {
        var line = $"{(done.Contains(o.Marker) ? "*" : " ")} {o.Row.Date:yyyy-MM-dd} {o.Marker,-26} {o.Customer}{(o.Payer is null ? "" : " c/o " + o.Payer)} | {o.Quantity} × {o.Item}"
                   + $"{(o.Shot ? " +shot" : "")}{(o.Oat ? " +oat" : "")} @ {o.Version.Name[Planner.VersionPrefix.Length..^1]} | {o.DiscountType}{(o.DiscountValue > 0 ? " " + M(o.DiscountValue) : "")}"
                   + $" | ₱{M(o.Total)}{(o.Shortfall > 0 ? $" (sheet ₱{M(o.Row.Total)})" : "")} | {(o.Pay ? $"pay {o.Channel} {o.PaidOn:yyyy-MM-dd}" : o.Total == 0 ? "nothing to pay" : "open")}";
        s.AppendLine(line);
    }

    return s.ToString();

    static string Kind(string text)
    {
        var cut = text.IndexOfAny(['"', ':', '₱', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9']);
        return (cut > 0 ? text[..cut] : text).Trim().TrimEnd(',', ';');
    }
}
