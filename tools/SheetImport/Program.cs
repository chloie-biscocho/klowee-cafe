using System.Globalization;
using System.Text;
using Klowee.SheetImport;

// Imports the owners' sales workbook (May 2025 – Nov 2026) through the admin
// API. Handover 13.
//
//   parse   Stage 1–2: read the office tabs, write docs/import/parsed.csv and
//           docs/import/summary.txt, and add new keys to customers.csv and
//           drinks.csv (values already there are kept). Writes nothing to the
//           API; signs in only to read the menu items for the drink pre-fill.
//
// Stages 3–7 (versions, customers, orders, payments, reconcile) follow the
// owner's review of the two mapping files.

const string Dir = "docs/import";
var workbookPath = args.SkipWhile(a => a != "--workbook").Skip(1).FirstOrDefault() ?? $"{Dir}/KLOWEE_CAFE_SALES_REPORT.xlsx";

if (args.FirstOrDefault() != "parse")
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/SheetImport -- parse [--workbook <path>]");
    Console.Error.WriteLine("Run from the repository root. Stages 3–7 come after the owner reviews the mapping files.");
    return 1;
}

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
    var items = await api.ItemsAsync();

    WriteParsed($"{Dir}/parsed.csv", rows);
    var importable = rows.Where(r => r.Skip.Length == 0).ToList();
    var customers = Mappings.WriteCustomers($"{Dir}/customers.csv", importable);
    var drinks = Mappings.WriteDrinks($"{Dir}/drinks.csv", importable, items);

    var summary = Summary(workbookPath, tabs, rows, customers, drinks, new Uri(apiUrl).Host, items.Count);
    File.WriteAllText($"{Dir}/summary.txt", summary, new UTF8Encoding(false));
    Console.Write(summary);
    return 0;
}
catch (ImportException ex)
{
    Console.Error.WriteLine($"Stopped: {ex.Message}");
    return 1;
}

static void WriteParsed(string path, List<ParsedRow> rows)
{
    string[] header =
    [
        "tab", "row", "date", "raw_name", "name", "payer", "raw_drink", "drink", "quantity", "unit_price",
        "extra_shot", "oat_milk", "total", "status", "raw_channel", "channel", "discount", "discount_value",
        "paid_on", "raw_paid", "notes", "skip", "flags",
    ];
    static string M(decimal d) => d.ToString("0.##", CultureInfo.InvariantCulture);
    Csv.Write(path, rows.Select(r => (IReadOnlyList<string>)
    [
        r.Tab, r.Row.ToString(CultureInfo.InvariantCulture), r.Date.ToString("yyyy-MM-dd"), r.RawName, r.Name, r.Payer,
        r.RawDrink, r.Drink, r.Quantity.ToString(CultureInfo.InvariantCulture), M(r.UnitPrice), M(r.ExtraShot), M(r.OatMilk),
        M(r.Total), r.Status, r.RawChannel, r.Channel, r.Discount, M(r.DiscountValue),
        r.PaidOn?.ToString("yyyy-MM-dd") ?? "", r.RawPaid, r.Notes, r.Skip, string.Join("; ", r.Flags),
    ]).Prepend(header));
}

static string Summary(string workbook, List<TabResult> tabs, List<ParsedRow> rows,
    (int Total, int Added) customers, (int Total, int Added) drinks, string host, int itemCount)
{
    var s = new StringBuilder();
    var importable = rows.Where(r => r.Skip.Length == 0).ToList();
    static string Where(ParsedRow r) => $"{r.Tab}:{r.Row}";
    static string M(decimal d) => d.ToString("#,0.##", CultureInfo.InvariantCulture);

    s.AppendLine($"Workbook  {workbook}");
    s.AppendLine($"Menu      {itemCount} items (archived included) from {host}");
    s.AppendLine();
    s.AppendLine("TABS");
    foreach (var tab in tabs.Where(t => t.Fate == "read")) s.AppendLine($"  read     {tab.Tab,-34} {tab.Rows,4} rows");
    foreach (var group in tabs.Where(t => t.Fate != "read").GroupBy(t => t.Fate))
        s.AppendLine($"  {group.Key}:\n           {string.Join(", ", group.Select(t => t.Tab))}");

    s.AppendLine();
    s.AppendLine($"ROWS      {rows.Count} parsed, {importable.Count} importable (₱{M(importable.Sum(r => r.Total))}), {rows.Count - importable.Count} skipped");
    s.AppendLine($"  by status   {string.Join(", ", importable.GroupBy(r => r.Status.Length == 0 ? "(none)" : r.Status).Select(g => $"{g.Key} {g.Count()}"))}");
    s.AppendLine($"  by channel  {string.Join(", ", importable.GroupBy(r => r.Channel.Length == 0 ? "(none)" : r.Channel).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}"))}");
    s.AppendLine($"  discounts   {string.Join(", ", importable.GroupBy(r => r.Discount).Select(g => $"{g.Key} {g.Count()}"))}");
    s.AppendLine($"  with payer  {importable.Count(r => r.Payer.Length > 0)}");

    s.AppendLine();
    s.AppendLine("SKIPPED (cannot become an order)");
    foreach (var group in rows.Where(r => r.Skip.Length > 0).GroupBy(r => r.Skip))
    {
        s.AppendLine($"  {group.Count(),3}  {group.Key}");
        foreach (var r in group) s.AppendLine($"         {Where(r),-18} {r.RawName} | {r.RawDrink} | ₱{M(r.Total)} | {r.Notes}");
    }

    s.AppendLine();
    s.AppendLine("FLAGGED (importable, but worth a look)");
    foreach (var group in importable.SelectMany(r => r.Flags.Select(f => (Row: r, Flag: f)))
                 .GroupBy(x => Kind(x.Flag)).OrderByDescending(g => g.Count()))
    {
        s.AppendLine($"  {group.Count(),3}  {group.Key}");
        foreach (var x in group) s.AppendLine($"         {Where(x.Row),-18} {x.Row.RawName} | {x.Row.RawDrink}: {x.Flag}");
    }

    s.AppendLine();
    s.AppendLine("MAPPING FILES (blank means the rows using it are skipped)");
    s.AppendLine($"  customers.csv  {customers.Total} names, {customers.Added} new this run");
    s.AppendLine($"  drinks.csv     {drinks.Total} (drink, unit price) pairs, {drinks.Added} new this run");
    return s.ToString();

    // Groups flags by their wording before any quoted or numeric detail.
    static string Kind(string flag)
    {
        var cut = flag.IndexOfAny(['"', ':', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9']);
        var head = (cut > 0 ? flag[..cut] : flag).Trim().TrimEnd(',', ';');
        return head.StartsWith("payer") ? "payer named twice, differently" : head;
    }
}
