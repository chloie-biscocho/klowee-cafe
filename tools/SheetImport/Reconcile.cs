using System.Globalization;
using System.Text;

namespace Klowee.SheetImport;

/// <summary>
/// Stage 7: the sheet's own figures next to what the import carries (planned
/// in a dry run, read back from the API after an apply), with the reasons for
/// every difference: skipped rows and amounts an order couldn't carry.
/// </summary>
public static class Reconcile
{
    public static string Table(
        List<TabResult> tabs, List<ParsedRow> rows, ImportPlan plan,
        Dictionary<string, decimal>? apiRevenueByTab, Dictionary<string, decimal>? apiOutstanding)
    {
        var s = new StringBuilder();
        static string M(decimal? d) => d is null ? "–" : d.Value.ToString("#,0.##", CultureInfo.InvariantCulture);
        var imported = plan.Orders.GroupBy(o => o.Row.Tab).ToDictionary(g => g.Key, g => g.Sum(o => o.Total));
        var shortfall = plan.Orders.GroupBy(o => o.Row.Tab).ToDictionary(g => g.Key, g => g.Sum(o => o.Shortfall));
        var skipped = plan.Skipped.GroupBy(k => k.Row.Tab).ToDictionary(g => g.Key, g => g.Sum(k => k.Row.Total));
        var api = apiRevenueByTab is not null;

        s.AppendLine("REVENUE BY MONTH (₱)");
        s.AppendLine($"  {"Tab",-16} {"Sheet cell",11} {"Sheet rows",11} {"Skipped",9} {"Not carried",11} {(api ? "API" : "Planned"),11} {"Cell − " + (api ? "API" : "plan"),12}");
        decimal tCell = 0, tCellCarried = 0, tRows = 0, tSkip = 0, tShort = 0, tImp = 0;
        foreach (var tab in tabs.Where(t => t.Fate == "read"))
        {
            var sheetRows = rows.Where(r => r.Tab == tab.Tab).Sum(r => r.Total);
            var carried = api ? apiRevenueByTab!.GetValueOrDefault(tab.Tab) : imported.GetValueOrDefault(tab.Tab);
            s.AppendLine($"  {tab.Tab,-16} {M(tab.Revenue),11} {M(sheetRows),11} {M(skipped.GetValueOrDefault(tab.Tab)),9} {M(shortfall.GetValueOrDefault(tab.Tab)),11} {M(carried),11} {M(tab.Revenue - carried),12}");
            if (tab.Revenue is not null) { tCell += tab.Revenue.Value; tCellCarried += carried; }
            tRows += sheetRows; tSkip += skipped.GetValueOrDefault(tab.Tab);
            tShort += shortfall.GetValueOrDefault(tab.Tab); tImp += carried;
        }

        s.AppendLine($"  {"Total",-16} {M(tCell),11} {M(tRows),11} {M(tSkip),9} {M(tShort),11} {M(tImp),11} {M(tCell - tCellCarried),12}");
        s.AppendLine("  Sheet rows − skipped − not carried = imported. The cell total covers only tabs that have the cell; where a cell differs from its rows, the sheet disagrees with itself.");

        // Open tabs: sheet = unpaid rows' sheet totals by whoever pays; skipped unpaid rows can't be on a tab.
        var sheetOpen = plan.Orders.Where(o => !o.Pay && o.Row.Status is "Unpaid" or "Awaiting")
            .GroupBy(o => o.TabOwner, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(o => o.Row.Total), StringComparer.OrdinalIgnoreCase);
        var planOpen = plan.Orders.Where(o => !o.Pay && o.Total > 0 && o.Row.Status is "Unpaid" or "Awaiting")
            .GroupBy(o => o.TabOwner, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(o => o.Total), StringComparer.OrdinalIgnoreCase);
        var carriedOpen = apiOutstanding ?? planOpen;
        var names = sheetOpen.Keys.Concat(carriedOpen.Where(c => c.Value != 0).Select(c => c.Key)).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();

        s.AppendLine();
        s.AppendLine("OPEN TABS BY CUSTOMER (₱; the tab belongs to whoever pays)");
        s.AppendLine($"  {"Customer",-18} {"Sheet",9} {(api ? "API" : "Planned"),9} {"Diff",8}");
        foreach (var name in names)
        {
            var sheet = sheetOpen.GetValueOrDefault(name);
            var carried = carriedOpen.GetValueOrDefault(name);
            s.AppendLine($"  {name,-18} {M(sheet),9} {M(carried),9} {(sheet == carried ? "" : M(sheet - carried)),8}");
        }

        s.AppendLine($"  {"Total",-18} {M(sheetOpen.Values.Sum()),9} {M(names.Sum(n => carriedOpen.GetValueOrDefault(n))),9}");
        var skippedOpen = plan.Skipped.Where(k => k.Row.Status is "Unpaid" or "Awaiting" && k.Row.Total > 0).ToList();
        if (skippedOpen.Count > 0)
        {
            s.AppendLine($"  Unpaid rows skipped, so on no tab: {string.Join("; ", skippedOpen.Select(k => $"{k.Row.Tab}:{k.Row.Row} {k.Row.RawName} ₱{M(k.Row.Total)}"))}");
        }

        return s.ToString();
    }
}
