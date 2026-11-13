using System.Globalization;

namespace Klowee.SheetImport;

/// <summary>An office version for history: the regular price of every drink sold so far, as of its first day.</summary>
public sealed record PlannedVersion(string Name, DateOnly EffectiveFrom, Dictionary<string, decimal> Prices, List<string> Changes);

public sealed record PlannedOrder
{
    public required ParsedRow Row { get; init; }
    public required string Customer { get; init; }
    public required string? Payer { get; init; }
    public required string Item { get; init; }
    public required PlannedVersion Version { get; init; }
    public required int Quantity { get; init; }
    public required bool Shot { get; init; }
    public required bool Oat { get; init; }
    public required string DiscountType { get; init; }
    public required decimal DiscountValue { get; init; }
    /// <summary>
    /// A free drink: use a Coffee Club free-drink reward on the line if the
    /// customer has one when the order is imported; otherwise a Fixed
    /// discount of <see cref="FallbackDiscount"/>. The old LoyaltyFree order
    /// discount is history only and refused for new orders (decision 020).
    /// </summary>
    public bool FreeDrinkReward { get; init; }
    public decimal FallbackDiscount { get; init; }
    /// <summary>What the API will compute: the sheet's Total unless a shortfall is listed.</summary>
    public required decimal Total { get; init; }
    /// <summary>Sheet Total minus <see cref="Total"/>: money the sheet shows that the order can't carry.</summary>
    public required decimal Shortfall { get; init; }
    public required bool Pay { get; init; }
    public required string Channel { get; init; }
    public required DateOnly PaidOn { get; init; }
    public required string Notes { get; init; }
    public required string? PaymentNotes { get; init; }
    public required List<string> Decisions { get; init; }

    public string Marker => Planner.Marker(Row);
    /// <summary>The tab belongs to whoever pays.</summary>
    public string TabOwner => Payer ?? Customer;
}

public sealed record SkippedRow(ParsedRow Row, string Reason);

public sealed record ImportPlan(
    List<PlannedVersion> Versions,
    List<PlannedOrder> Orders,
    List<SkippedRow> Skipped,
    List<string> Items,
    List<string> Customers);

/// <summary>
/// Turns parsed rows and the owner's two mapping files into a plan: historical
/// versions, then one order per row in date order, with the discount and
/// payment each needs so its total equals the sheet's. Pure: no API calls.
/// </summary>
public static class Planner
{
    public const string VersionPrefix = "Imported office menu (from ";

    public static string Marker(ParsedRow row) => $"[import:{row.Tab}:{row.Row}]";
    public static string VersionName(DateOnly from) => $"{VersionPrefix}{from:yyyy-MM-dd})";

    private static readonly HashSet<string> ApiChannels = ["Cash", "GCash", "BusinessGCash", "CIMB", "BPI", "Other"];

    public static ImportPlan Build(
        IReadOnlyList<ParsedRow> rows,
        IReadOnlyDictionary<string, string> customerMap,
        IReadOnlyDictionary<string, string> drinkMap,
        decimal shotPrice, decimal oatPrice)
    {
        var skipped = new List<SkippedRow>();
        var resolved = new List<(ParsedRow Row, string Customer, string? Payer, string Item)>();

        foreach (var row in rows.OrderBy(r => r.Date).ThenBy(r => Array.IndexOf(Workbook.OfficeTabs, r.Tab)).ThenBy(r => r.Row))
        {
            if (row.Skip.Length > 0) { skipped.Add(new(row, row.Skip)); continue; }

            var customer = Lookup(customerMap, Mappings.Key(row.Name), "customer", row.Name, out var why);
            var payer = why is null && row.Payer.Length > 0 ? Lookup(customerMap, Mappings.Key(row.Payer), "payer", row.Payer, out why) : null;
            var item = why is null ? Lookup(drinkMap, DrinkKey(row.Drink, row.UnitPrice), "drink", $"{row.Drink} at ₱{Money(row.UnitPrice)}", out why) : null;
            if (why is not null) { skipped.Add(new(row, why)); continue; }

            resolved.Add((row, customer!, payer == customer ? null : payer, item!));
        }

        var versions = BuildVersions(resolved.Select(r => (r.Row, r.Item)).ToList());
        var orders = new List<PlannedOrder>();

        foreach (var (row, customer, payer, item) in resolved)
        {
            var decisions = new List<string>();
            var month = new DateOnly(row.Date.Year, row.Date.Month, 1);
            var version = versions.LastOrDefault(v => v.EffectiveFrom <= month && v.Prices.ContainsKey(item))
                ?? versions.Where(v => v.Prices.ContainsKey(item)).MinBy(v => Math.Abs(v.EffectiveFrom.DayNumber - month.DayNumber));
            if (version is null) { skipped.Add(new(row, $"no regular price known for {item} anywhere in the sheet")); continue; }
            if (version.EffectiveFrom > month) decisions.Add($"{item} first priced in \"{version.Name}\"; used that");

            var quantity = row.Quantity;
            var price = version.Prices[item];
            if (row.Discount == "None" && row.UnitPrice > price)
            {
                var monthPrices = versions.Where(v => v.EffectiveFrom == month).SelectMany(v => v.Prices.Where(p => p.Key == item).Select(p => p.Value))
                    .Append(price).Concat(SeenPrices(rows, item, drinkMap, month)).Distinct().ToList();
                var multiple = monthPrices.FirstOrDefault(c => c > 0 && row.UnitPrice % c == 0 && row.UnitPrice / c >= 2);
                // A price that was the regular one within three months (an early or late price change) borrows that version.
                var exact = versions.Where(v => v.Prices.GetValueOrDefault(item) == row.UnitPrice && Math.Abs(MonthsApart(v.EffectiveFrom, month)) <= 3)
                    .MinBy(v => Math.Abs(MonthsApart(v.EffectiveFrom, month)));
                if (multiple > 0)
                {
                    var k = (int)(row.UnitPrice / multiple);
                    quantity *= k;
                    decisions.Add($"₱{Money(row.UnitPrice)} is {k} × ₱{Money(multiple)}: imported as {k} drinks");
                }
                else if (exact is not null)
                {
                    version = exact;
                    price = row.UnitPrice;
                    decisions.Add($"₱{Money(row.UnitPrice)} was {item}'s price in \"{exact.Name}\"; used that version");
                }
            }

            var shot = row.ExtraShot > 0;
            var oat = row.OatMilk > 0;
            var unit = price + (shot ? shotPrice : 0) + (oat ? oatPrice : 0);
            var subtotal = unit * quantity;
            var sheet = row.Total;

            string type = "None";
            decimal value = 0, total = subtotal, fallback = 0;
            var freeDrink = false;
            if (sheet < subtotal)
            {
                var off = subtotal - sheet;
                switch (row.Discount)
                {
                    case "Free":
                        // A Coffee Club free drink frees one drink's price; add-ons stay
                        // charged, so any gap to the sheet (it freed the add-ons too) is a Fixed discount.
                        freeDrink = true;
                        fallback = off;
                        var afterReward = subtotal - price;
                        (type, value, total) = afterReward > sheet ? ("Fixed", afterReward - sheet, sheet) : ("None", 0m, afterReward);
                        decisions.Add($"free drink: a Coffee Club free-drink reward if {customer} has one at import, else ₱{Money(off)} off (flag)");
                        break;
                    case "Percent" when Math.Round(subtotal * row.DiscountValue / 100, 2, MidpointRounding.AwayFromZero) == off:
                        (type, value, total) = ("Percent", row.DiscountValue, sheet);
                        break;
                    case "Fixed" when row.DiscountValue == off:
                        (type, value, total) = ("Fixed", off, sheet);
                        break;
                    case "None" when AddOnGap(row, quantity, shotPrice, oatPrice) == off:
                        (type, value, total) = ("Fixed", off, sheet);
                        decisions.Add($"add-ons cost less then (oat milk ₱{Money(row.OatMilk)}, shot ₱{Money(row.ExtraShot)}) than today: ₱{Money(off)} off");
                        break;
                    default:
                        (type, value, total) = ("Fixed", off, sheet);
                        decisions.Add(row.Discount == "None"
                            ? $"charged ₱{Money(sheet)}, below ₱{Money(subtotal)} at that menu's prices: ₱{Money(off)} off"
                            : $"notes say {row.Discount.ToLowerInvariant()} {Money(row.DiscountValue)}, but the sheet total needs ₱{Money(off)} off: recorded as that");
                        break;
                }
            }

            var shortfall = sheet - total;
            if (shortfall > 0)
                decisions.Add($"sheet charged ₱{Money(sheet)}, more than ₱{Money(subtotal)} at that menu's prices: ₱{Money(shortfall)} not carried");

            var paid = row.Status is "Paid" or "";
            var pay = paid && total > 0;
            var channel = ApiChannels.Contains(row.Channel) ? row.Channel : "Other";
            if (row.Status == "") decisions.Add("no status (May 2025): imported as paid");

            var notes = (Marker(row) + " " + row.Notes).Trim();
            orders.Add(new PlannedOrder
            {
                Row = row, Customer = customer, Payer = payer == customer ? null : payer, Item = item, Version = version,
                Quantity = quantity, Shot = shot, Oat = oat, DiscountType = type, DiscountValue = value,
                FreeDrinkReward = freeDrink, FallbackDiscount = fallback,
                Total = total, Shortfall = shortfall, Pay = pay, Channel = channel,
                PaidOn = row.PaidOn ?? row.Date,
                Notes = notes.Length > 1000 ? notes[..1000] : notes,
                PaymentNotes = channel == row.Channel ? Marker(row) : $"{Marker(row)} sheet channel: {(row.RawChannel.Length > 0 ? row.RawChannel : "none")}",
                Decisions = decisions,
            });
        }

        var items = resolved.Select(r => r.Item).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(i => i).ToList();
        var customers = resolved.SelectMany(r => r.Payer is null ? [r.Customer] : new[] { r.Customer, r.Payer })
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
        return new ImportPlan(versions, orders, skipped, items, customers);
    }

    /// <summary>
    /// One version per month in which some drink's regular price changed (or a
    /// drink first appeared). A month's price is its most common regular price,
    /// the higher on a tie; regular means no discount, or a free drink whose
    /// price column still shows the price. A drink never sold at a regular price
    /// takes its price from a discounted row instead.
    /// </summary>
    private static List<PlannedVersion> BuildVersions(List<(ParsedRow Row, string Item)> rows)
    {
        var seen = new Dictionary<string, Dictionary<DateOnly, List<decimal>>>(StringComparer.OrdinalIgnoreCase);
        void See(string item, DateOnly date, decimal price)
        {
            var month = new DateOnly(date.Year, date.Month, 1);
            if (!seen.TryGetValue(item, out var byMonth)) seen[item] = byMonth = [];
            if (!byMonth.TryGetValue(month, out var list)) byMonth[month] = list = [];
            list.Add(price);
        }

        foreach (var (row, item) in rows.Where(r => r.Row.Discount is "None" or "Free" && r.Row.UnitPrice > 0))
            See(item, row.Date, row.UnitPrice);

        foreach (var (row, item) in rows.Where(r => !seen.ContainsKey(r.Item) && r.Row.Discount == "Percent" && r.Row.DiscountValue < 100 && r.Row.UnitPrice > 0))
        {
            // The price column sometimes already has the discount taken off (₱105 at 50% off, total ₱105).
            var taken = row.UnitPrice * row.Quantity + row.ExtraShot + row.OatMilk == row.Total;
            See(item, row.Date, taken ? Math.Round(row.UnitPrice / (1 - row.DiscountValue / 100), 2) : row.UnitPrice);
        }

        var months = rows.Select(r => new DateOnly(r.Row.Date.Year, r.Row.Date.Month, 1)).Distinct().Order().ToList();
        var current = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var versions = new List<PlannedVersion>();
        foreach (var month in months)
        {
            var changes = new List<string>();
            foreach (var (item, byMonth) in seen.OrderBy(s => s.Key))
            {
                if (!byMonth.TryGetValue(month, out var prices)) continue;
                var groups = prices.GroupBy(p => p).ToList();
                var top = groups.Where(g => g.Count() == groups.Max(x => x.Count())).Max(g => g.Key);
                if (current.TryGetValue(item, out var was) && was == top) continue;
                changes.Add(current.ContainsKey(item) ? $"{item} ₱{Money(was)} → ₱{Money(top)}" : $"{item} ₱{Money(top)} (new)");
                current[item] = top;
            }

            if (changes.Count > 0) versions.Add(new PlannedVersion(VersionName(month), month, new(current, StringComparer.OrdinalIgnoreCase), changes));
        }

        return versions;
    }

    private static int MonthsApart(DateOnly a, DateOnly b) => (a.Year - b.Year) * 12 + a.Month - b.Month;

    /// <summary>How much less the sheet charged for add-ons than today's prices.</summary>
    private static decimal AddOnGap(ParsedRow row, int quantity, decimal shotPrice, decimal oatPrice) =>
        (row.ExtraShot > 0 ? shotPrice * quantity - row.ExtraShot : 0) + (row.OatMilk > 0 ? oatPrice * quantity - row.OatMilk : 0);

    private static IEnumerable<decimal> SeenPrices(IReadOnlyList<ParsedRow> rows, string item, IReadOnlyDictionary<string, string> drinkMap, DateOnly month) =>
        rows.Where(r => r.Skip.Length == 0 && r.Discount == "None" && r.UnitPrice > 0 && r.Date.Year == month.Year && r.Date.Month == month.Month
                        && string.Equals(drinkMap.GetValueOrDefault(DrinkKey(r.Drink, r.UnitPrice)), item, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.UnitPrice);

    private static string? Lookup(IReadOnlyDictionary<string, string> map, string key, string what, string shown, out string? why)
    {
        why = null;
        if (!map.TryGetValue(key, out var value)) { why = $"{what} \"{shown}\" is not in the mapping file (run parse)"; return null; }
        if (value.Length == 0) { why = $"{what} \"{shown}\" is blank in the mapping file"; return null; }
        return value;
    }

    public static string DrinkKey(string drink, decimal unitPrice) => $"{Mappings.Key(drink)}|{Money(unitPrice)}";

    public static string Money(decimal d) => d.ToString("0.##", CultureInfo.InvariantCulture);
}
