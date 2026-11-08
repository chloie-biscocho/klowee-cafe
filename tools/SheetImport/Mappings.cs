using System.Globalization;

namespace Klowee.SheetImport;

/// <summary>
/// The two files the owner edits: raw customer names → canonical names, and
/// (raw drink, unit price) → menu item. A blank canonical value means "skip the
/// rows that use it". Re-running the parser keeps every value already in the
/// files and only adds keys it hasn't seen.
/// </summary>
public static class Mappings
{
    public static readonly string[] CustomerHeader = ["raw_name", "canonical_name", "rows", "hint"];
    public static readonly string[] DrinkHeader = ["raw_drink", "unit_price", "canonical_item", "rows", "hint"];

    /// <summary>Drinks renamed in handover 12a, under their old names.</summary>
    private static readonly Dictionary<string, string> Renamed = new() { ["ubelatte"] = "Classic Ube Latte" };

    public static (int Total, int Added) WriteCustomers(string path, IReadOnlyList<ParsedRow> rows)
    {
        var uses = rows.SelectMany(r => r.Payer.Length > 0 ? new[] { r.Name, r.Payer } : [r.Name])
            .GroupBy(Key).ToList();

        // Same name ignoring case and spacing: the most used spelling is canonical.
        var canonical = uses.ToDictionary(g => g.Key, g => g.GroupBy(n => n).OrderByDescending(s => s.Count()).ThenBy(s => s.Key, StringComparer.Ordinal).First().Key);
        var counts = uses.ToDictionary(g => g.Key, g => g.Count());

        var lines = new List<string[]>();
        foreach (var group in uses.OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            foreach (var spelling in group.Distinct().OrderBy(s => s, StringComparer.Ordinal))
            {
                var target = canonical[group.Key];
                var hint = spelling == target ? "" : "same name, different case or spacing";

                var (variantOf, why) = Variant(group.Key, counts);
                if (variantOf is not null)
                {
                    target = canonical[variantOf];
                    hint = $"{why} {target}: same person? check";
                }
                else if (char.IsDigit(spelling[^1]))
                {
                    hint = "numbered name: kept apart; one person or several?";
                }
                else if (spelling.IndexOfAny(['(', '&', '/']) >= 0)
                {
                    hint = "not a single plain name: check";
                }
                else if (!rows.Any(r => Key(r.Name) == group.Key))
                {
                    hint = "only ever a payer (c/o): map to a customer, or blank to skip those rows";
                }

                lines.Add([spelling, target, group.Count(s => s == spelling).ToString(CultureInfo.InvariantCulture), hint]);
            }
        }

        return Merge(path, CustomerHeader, lines, keyColumns: 1, valueColumn: 1);
    }

    private static readonly string[] Honorifics = ["ate ", "kuya ", "ma'am ", "maam ", "sir ", "boss "];

    /// <summary>
    /// The obvious variants, pointed at the more used name: one letter apart at
    /// the end (Lads/Ladz, Yelly/Yelley), a shortened name (Klo/Klowe, Ian/Ianberg),
    /// or an honorific (Ate Gaga/Gaga). Names with numbers are never merged.
    /// </summary>
    private static (string? Of, string Why) Variant(string key, Dictionary<string, int> counts)
    {
        if (key.Any(char.IsDigit)) return (null, "");

        var honorific = Honorifics.FirstOrDefault(key.StartsWith);
        if (honorific is not null && counts.ContainsKey(key[honorific.Length..])) return (key[honorific.Length..], "honorific dropped:");

        var others = counts.Keys.Where(k => k != key && !k.Any(char.IsDigit) && counts[k] >= counts[key]).OrderByDescending(k => counts[k]).ToList();
        var spelling = others.Where(k => Math.Min(k.Length, key.Length) >= 4 && k[..3] == key[..3] && Distance(k, key) <= 1).ToList();
        if (spelling.Count > 0) return (spelling[0], "one letter from"); // the most used

        var longer = others.Where(k => key.Length >= 3 && k.Length > key.Length && k.StartsWith(key) && !k.Contains(' ')).ToList();
        if (longer.Count == 1) return (longer[0], "short for");
        return (null, "");
    }

    public static (int Total, int Added) WriteDrinks(string path, IReadOnlyList<ParsedRow> rows, IReadOnlyList<Item> items)
    {
        var lines = rows
            .GroupBy(r => (Key(r.Drink), r.UnitPrice))
            .OrderBy(g => g.Key.Item1, StringComparer.Ordinal).ThenBy(g => g.Key.UnitPrice)
            .Select(g =>
            {
                var spelling = g.GroupBy(r => r.Drink).OrderByDescending(s => s.Count()).First().Key;
                var (item, hint) = MatchItem(spelling, items);
                if (spelling.Any(c => char.IsDigit(c) || c == '₱')) hint = (hint + "; the text carries a price or count: check").TrimStart(';', ' ');
                return new[] { spelling, g.Key.UnitPrice.ToString("0.##", CultureInfo.InvariantCulture), item, g.Count().ToString(CultureInfo.InvariantCulture), hint };
            })
            .ToList();

        return Merge(path, DrinkHeader, lines, keyColumns: 2, valueColumn: 2);
    }

    /// <summary>
    /// Conservative: an exact match (ignoring case, spaces and punctuation), an
    /// old name, the same words in another order, one letter off, or a unique
    /// partial match (with a hint). Two candidates or none leave it blank.
    /// </summary>
    public static (string Item, string Hint) MatchItem(string drink, IReadOnlyList<Item> items)
    {
        var d = Letters(drink);
        if (d.Length == 0) return ("", "");

        var exact = items.FirstOrDefault(i => Letters(i.Name) == d);
        if (exact is not null) return (exact.Name, exact.IsArchived ? "archived item" : "");
        if (Renamed.TryGetValue(d, out var renamed)) return (renamed, "renamed in Nov 2026");

        var words = Words(drink);
        var sameWords = items.Where(i => Words(i.Name).SetEquals(words)).ToList();
        if (sameWords.Count == 1) return (sameWords[0].Name, "same words, other order: check");

        var oneOff = items.Where(i => d.Length >= 8 && Distance(Letters(i.Name), d) <= 1).ToList();
        if (oneOff.Count == 1) return (oneOff[0].Name, "spelling: check");

        // "Strawberry Matcha" is now a Houseblend and an Okumidori drink.
        var blends = items.Where(i => Letters(StripBlend(i.Name)) == d).ToList();
        if (blends.Count > 1) return ("", "candidates: " + string.Join(" / ", blends.Select(i => i.Name)));

        var partial = items.Where(i =>
        {
            var n = Letters(i.Name);
            return Math.Min(n.Length, d.Length) >= 6 && (n.Contains(d) || d.Contains(n));
        }).ToList();
        return partial.Count switch
        {
            1 => (partial[0].Name, "partial match: check"),
            > 1 => ("", "candidates: " + string.Join(" / ", partial.Select(i => i.Name))),
            _ => ("", "no menu item matches"),
        };
    }

    /// <summary>Reads the owner's values back: key columns joined by '|' (case-insensitive) → value.</summary>
    public static Dictionary<string, string> Load(string path, int keyColumns, int valueColumn)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return map;
        foreach (var row in Csv.Read(path).Skip(1))
        {
            var cells = row.Concat(Enumerable.Repeat("", 5)).ToList();
            map[string.Join('|', cells.Take(keyColumns).Select(c => c.Trim()))] = cells[valueColumn].Trim();
        }

        return map;
    }

    private static (int Total, int Added) Merge(string path, string[] header, List<string[]> fresh, int keyColumns, int valueColumn)
    {
        var existing = Load(path, keyColumns, valueColumn);
        var added = 0;
        foreach (var line in fresh)
        {
            var key = string.Join('|', line.Take(keyColumns));
            if (existing.TryGetValue(key, out var kept)) line[valueColumn] = kept; // the owner's word stands
            else added++;
        }

        Csv.Write(path, fresh.Prepend(header));
        return (fresh.Count, added);
    }

    public static string Key(string name) => string.Join(' ', name.Trim().TrimEnd('.', ',').ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Letters(string s) => new(s.ToLowerInvariant().Where(char.IsLetter).ToArray());

    private static HashSet<string> Words(string s) =>
        s.ToLowerInvariant().Split([' ', '-', '(', ')', '/'], StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    private static string StripBlend(string name) => name.Replace("(Houseblend)", "").Replace("(Okumidori)", "");

    private static int Distance(string a, string b)
    {
        var prev = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var cur = new int[b.Length + 1];
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            prev = cur;
        }

        return prev[b.Length];
    }
}

public static class MappingFiles
{
    /// <summary>customers.csv as raw-name key → canonical name ("" when blank).</summary>
    public static Dictionary<string, string> Customers(string path) => Read(path, row => Mappings.Key(row[0]), 1);

    /// <summary>drinks.csv as (drink, unit price) key → menu item name ("" when blank).</summary>
    public static Dictionary<string, string> Drinks(string path) => Read(path,
        row => Planner.DrinkKey(row[0], decimal.Parse(row[1], System.Globalization.CultureInfo.InvariantCulture)), 2);

    private static Dictionary<string, string> Read(string path, Func<List<string>, string> key, int valueColumn)
    {
        if (!File.Exists(path)) throw new ImportException($"{path} not found: run parse first.");
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Csv.Read(path).Skip(1))
        {
            var cells = row.Concat(Enumerable.Repeat("", 5)).ToList();
            var k = key(cells);
            var value = cells[valueColumn].Trim();
            if (map.TryGetValue(k, out var other) && !other.Equals(value, StringComparison.OrdinalIgnoreCase))
                throw new ImportException($"{Path.GetFileName(path)}: \"{cells[0]}\" is mapped twice, to \"{other}\" and \"{value}\".");
            map[k] = value;
        }

        return map;
    }
}
