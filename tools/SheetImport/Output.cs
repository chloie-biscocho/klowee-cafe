using System.Globalization;
using System.Text;

namespace Klowee.SheetImport;

/// <summary>Stage 1's two outputs: every parsed row, and a summary of what was read, skipped and flagged.</summary>
public static class Output
{
    public static void WriteParsed(string path, List<ParsedRow> rows)
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

    public static string Summary(string workbook, List<TabResult> tabs, List<ParsedRow> rows,
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
}
