using System.IO.Compression;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace Klowee.SheetImport;

/// <summary>One cell row of a monthly tab, before any interpretation.</summary>
public sealed record SheetRow(
    string Tab, int Row, DateOnly Date,
    string Name, string Drink, object? Price, object? ExtraShot, object? OatMilk, object? Total,
    string Status, string Channel, string Notes, object? DatePaid);

/// <param name="Revenue">The tab's own "Total Revenue" cell (A3), when it has one.</param>
/// <param name="ArTotal">The tab's own "AR Total" cell, when it has one.</param>
public sealed record TabResult(string Tab, string Fate, int Rows, decimal? Revenue = null, decimal? ArTotal = null);

/// <summary>
/// Reads the office-sales tabs of the owners' workbook. The workbook holds
/// several copies of the same months (early drafts, "(Manual)" working copies
/// and the final monthly tabs), so the tabs to read are listed explicitly; every
/// other tab is reported with the reason it was left out.
/// </summary>
public static class Workbook
{
    /// <summary>The authoritative tab for each month, May 2025 to Nov 2026.</summary>
    public static readonly string[] OfficeTabs =
    [
        "May(Manual)", "June 2025", "July 2025", "August 2025", "September 2025", "October 2025",
        "November 2025", "December 2025", "Jan 2026", "Feb 2026", "March 2026", "April 2026",
        "May 2026", "June 2026", "July 2026", "August 2026", "September 2026", "October 2026", "Nov 2026",
    ];

    /// <summary>Tabs from March 2026 on have no header row; their columns are fixed.</summary>
    private static readonly string[] FixedLayout =
    [
        "date", "name", "order", "price", "extra shot", "sub milk (oat)", "total amount",
        "order status", "mode of payment", "notes", "date paid (ar)",
    ];

    private static readonly Dictionary<string, string[]> Synonyms = new()
    {
        ["date"] = ["date"],
        ["name"] = ["name", "customer name"],
        ["order"] = ["order"],
        ["price"] = ["price"],
        ["shot"] = ["extra shot"],
        ["oat"] = ["sub milk (oat)", "extra milk"],
        ["total"] = ["total amount", "total"],
        ["status"] = ["order status"],
        ["channel"] = ["mode of payment"],
        ["notes"] = ["notes"],
        ["paid"] = ["date paid (ar)"],
    };

    public static (List<SheetRow> Rows, List<TabResult> Tabs) Read(string path)
    {
        using var workbook = new XLWorkbook(WithoutDropdownsOrDrawings(path));
        var rows = new List<SheetRow>();
        var tabs = new List<TabResult>();

        foreach (var sheet in workbook.Worksheets)
        {
            if (!OfficeTabs.Contains(sheet.Name))
            {
                tabs.Add(new TabResult(sheet.Name, WhyIgnored(sheet.Name), 0));
                continue;
            }

            var tabRows = ReadTab(sheet);
            rows.AddRange(tabRows);
            var (revenue, ar) = SummaryCells(sheet);
            tabs.Add(new TabResult(sheet.Name, "read", tabRows.Count, revenue, ar));
        }

        var missing = OfficeTabs.Except(workbook.Worksheets.Select(s => s.Name)).ToList();
        if (missing.Count > 0)
        {
            throw new ImportException($"Workbook is missing tab(s): {string.Join(", ", missing)}.");
        }

        return (rows, tabs);
    }

    /// <summary>
    /// ClosedXML refuses two things Google Sheets exports allow: a dropdown (data
    /// validation) list over Excel's 255-character limit, and two pictures with
    /// the same id. The import needs neither, so dropdowns and drawings are removed
    /// from an in-memory copy. The owner's file is never changed.
    /// </summary>
    private static MemoryStream WithoutDropdownsOrDrawings(string path)
    {
        if (!File.Exists(path)) throw new ImportException($"Workbook not found: {path}");
        var copy = new MemoryStream();
        using (var source = File.OpenRead(path)) source.CopyTo(copy);

        using (var zip = new ZipArchive(copy, ZipArchiveMode.Update, leaveOpen: true))
        {
            foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/") && e.FullName.EndsWith(".xml")).ToList())
            {
                Rewrite(zip, entry, xml => Regex.Replace(Regex.Replace(xml,
                    "<dataValidations\\b.*?</dataValidations>", "", RegexOptions.Singleline),
                    "<drawing\\b[^>]*/>", ""));
            }

            foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("xl/worksheets/_rels/")).ToList())
            {
                Rewrite(zip, entry, xml => Regex.Replace(xml, "<Relationship\\b[^>]*/drawing\"[^>]*/>", ""));
            }
        }

        copy.Position = 0;
        return copy;
    }

    private static void Rewrite(ZipArchive zip, ZipArchiveEntry entry, Func<string, string> change)
    {
        string xml;
        using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
        var changed = change(xml);
        if (changed == xml) return;
        var name = entry.FullName;
        entry.Delete();
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(changed);
    }

    /// <summary>
    /// A label row ("Total Revenue", "G-Cash Total", "AR Total", …) sits in rows
    /// 1–8 with its values two rows below; Total Revenue is in column A.
    /// October 2026's A1 says "F", so column A is read whenever the row carries totals.
    /// </summary>
    private static (decimal? Revenue, decimal? Ar) SummaryCells(IXLWorksheet sheet)
    {
        for (var r = 1; r <= 8; r++)
        {
            var labels = Enumerable.Range(1, 14).Select(c => Label(sheet, r, c)).ToList();
            if (!labels.Any(l => l.EndsWith(" total"))) continue;
            decimal? At(int c) => Value(sheet, r + 2, c) is double d ? (decimal)d : null;
            var ar = labels.FindIndex(l => l == "ar total");
            return (At(1), ar >= 0 ? At(ar + 1) : null);
        }

        return (null, null);
    }

    private static string WhyIgnored(string tab)
    {
        var lower = tab.ToLowerInvariant();
        var monthly = new[] { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" }
            .Any(m => lower.StartsWith(m)) && !lower.Contains("costing") && !lower.Contains("market");
        return monthly ? "ignored: duplicate or draft of a month read from another tab" : "ignored: not office sales";
    }

    private static List<SheetRow> ReadTab(IXLWorksheet sheet)
    {
        var (headerRow, columns) = FindHeader(sheet);
        int Col(string key) => columns.GetValueOrDefault(key);

        var last = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var result = new List<SheetRow>();
        DateOnly? date = null;

        for (var r = headerRow + 1; r <= last; r++)
        {
            // A date is written on the first row of each day; the rows below it share it.
            if (Value(sheet, r, Col("date")) is DateTime d)
            {
                date = DateOnly.FromDateTime(d);
            }

            var name = Text(sheet, r, Col("name"));
            var drink = Text(sheet, r, Col("order"));
            if (date is null || (name.Length == 0 && drink.Length == 0))
            {
                continue; // the summary block above the first sale, and blank rows
            }

            result.Add(new SheetRow(sheet.Name, r, date.Value, name, drink,
                Value(sheet, r, Col("price")), Value(sheet, r, Col("shot")), Value(sheet, r, Col("oat")),
                Value(sheet, r, Col("total")), Text(sheet, r, Col("status")), Text(sheet, r, Col("channel")),
                Text(sheet, r, Col("notes")), Value(sheet, r, Col("paid"))));
        }

        return result;
    }

    /// <summary>
    /// The header is the first row (of 15) with "date" in columns A–C. Requiring
    /// it that far left skips the "pay later" side panel, which has its own
    /// DATE / CUSTOMER NAME columns further right. No header means the fixed layout.
    /// </summary>
    private static (int Row, Dictionary<string, int> Columns) FindHeader(IXLWorksheet sheet)
    {
        for (var r = 1; r <= 15; r++)
        {
            var labels = Enumerable.Range(1, 14).Select(c => Label(sheet, r, c)).ToList();
            var start = labels.Take(3).ToList().IndexOf("date");
            if (start < 0 || !labels.Contains("order"))
            {
                continue;
            }

            var found = new Dictionary<string, int>();
            for (var i = start; i < labels.Count; i++)
            {
                if (labels[i].Length == 0)
                {
                    if (i > start && labels[i - 1].Length == 0) break; // two blanks end the table
                    continue;
                }

                foreach (var (key, names) in Synonyms)
                {
                    if (names.Contains(labels[i])) found.TryAdd(key, i + 1);
                }
            }

            // May 2025's tab has no channel header, but one row notes "GCASH" in the next column.
            found.TryAdd("channel", found.Values.Max() + 1);
            return (r, found);
        }

        var fixedColumns = Synonyms.ToDictionary(s => s.Key,
            s => Array.FindIndex(FixedLayout, label => s.Value.Contains(label)) + 1);
        return (0, fixedColumns);
    }

    private static string Label(IXLWorksheet sheet, int r, int c) =>
        Value(sheet, r, c) is string s ? s.Trim().ToLowerInvariant() : "";

    private static string Text(IXLWorksheet sheet, int r, int c) => Value(sheet, r, c) switch
    {
        null => "",
        string s => string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)),
        double n => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
        DateTime d => d.ToString("yyyy-MM-dd"),
        var other => other.ToString() ?? "",
    };

    /// <summary>The stored value (a formula's last result, as the owners saw it): string, double, DateTime, bool or null.</summary>
    private static object? Value(IXLWorksheet sheet, int r, int c)
    {
        if (c <= 0) return null;
        var cell = sheet.Cell(r, c);
        var v = cell.HasFormula ? cell.CachedValue : cell.Value;
        return v.Type switch
        {
            XLDataType.Blank => null,
            XLDataType.Text => v.GetText().Length == 0 ? null : v.GetText(),
            XLDataType.Number => v.GetNumber(),
            XLDataType.DateTime => v.GetDateTime(),
            XLDataType.Boolean => v.GetBoolean(),
            XLDataType.TimeSpan => null,
            _ => null, // error cells
        };
    }
}

public sealed class ImportException(string message) : Exception(message);
