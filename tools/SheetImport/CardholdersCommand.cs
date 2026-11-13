using System.Text;

namespace Klowee.SheetImport;

/// <summary>
/// `cardholders` (handover 16a, part 2): reads every order through the API,
/// finds the customers whose orders show a Coffee Club card reward, and
/// writes docs/import/loyalty-cardholders.csv for the owner to review.
/// Read-only: it writes nothing to the API.
/// </summary>
public static class CardholdersCommand
{
    private static readonly Guid CoffeeClub = new("c0ffee00-0000-4000-8000-000000000001");

    public static async Task<int> RunAsync(Api api, string path, TextWriter log)
    {
        var categories = (await api.CategoriesAsync()).ToDictionary(c => c.Id, c => c.LoyaltyClubId);
        var items = (await api.ItemsAsync()).ToDictionary(i => i.Id);
        Guid? ClubOf(Guid menuItemId) =>
            items.TryGetValue(menuItemId, out var item)
                ? item.LoyaltyMode switch
                {
                    "Club" => item.LoyaltyClubId,
                    "None" => null,
                    _ => categories.GetValueOrDefault(item.CategoryId),
                }
                : null;

        var summaries = new List<OrderSummary>();
        for (var page = 1; ; page++)
        {
            var batch = await api.OrdersAsync(new DateOnly(2025, 1, 1), DateOnly.FromDateTime(DateTime.Today).AddYears(1), page);
            summaries.AddRange(batch.Items);
            if (page * batch.PageSize >= batch.TotalCount) break;
        }

        var facts = new List<OrderFacts>();
        var zeroWithoutWords = 0;
        foreach (var (summary, index) in summaries.Select((s, i) => (s, i)))
        {
            var order = await api.FullOrderAsync(summary.Id);
            var notes = Cardholders.StripMarker(order.Notes);
            facts.Add(new OrderFacts(
                order.Customer.Name,
                order.OrderedOn,
                Cardholders.MarkerRow(order.Notes) ?? 100_000 + (summaries.Count - index),
                notes,
                order.DiscountType,
                order.DiscountValue,
                order.Status == "Paid",
                order.Items.Where(i => ClubOf(i.MenuItemId) == CoffeeClub).Sum(i => i.Quantity),
                order.Items.SelectMany(i => i.AddOns).Count(a => a.UnitPrice == 0 && a.Name.Contains("shot", StringComparison.OrdinalIgnoreCase)),
                order.Items.Where(i => i.Reward is not null).Select(i => i.Reward!.Type).ToList()));
            if (summary.Total == 0 && order.DiscountType == "Fixed" && notes.Length == 0) zeroWithoutWords++;
        }

        var rows = facts.GroupBy(f => f.Customer, StringComparer.OrdinalIgnoreCase)
            .Select(g => Cardholders.Plan(g.Key, g.ToList()))
            .Where(r => r is not null)
            .Select(r => r!)
            .OrderBy(r => r.Customer, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Csv.Write(path, new[] { (IReadOnlyList<string>)Cardholders.Header }.Concat(rows.Select(Cardholders.ToCsv)));

        // What matched, for the report.
        var matched = facts.Select(f => (f, Found: Cardholders.Detect(f))).ToList();
        var customers = await api.CustomersAsync();
        var s = new StringBuilder();
        s.AppendLine($"Orders read: {facts.Count}.");
        s.AppendLine("Patterns (orders matched):");
        foreach (var name in matched.SelectMany(m => m.Found.Select(e => e.Pattern)).Distinct().Order())
        {
            s.AppendLine($"  {matched.Count(m => m.Found.Any(e => e.Pattern == name)),4}  {name}");
        }

        s.AppendLine($"  {facts.Count(f => f.ShotsAtZero > 0),4}  extra shot at ₱0 (checked)");
        s.AppendLine($"Not counted: {zeroWithoutWords} orders at ₱0 by a Fixed discount with no wording (complimentary, mostly the owners').");
        s.AppendLine($"Candidates: {rows.Count} ({rows.Count(r => r.HasCard)} prefilled has_card = yes); flagged for a look: {rows.Count(r => r.Flag.Length > 0)}.");
        s.AppendLine($"Customers with no evidence at all: {customers.Count - rows.Count} of {customers.Count}.");
        s.AppendLine($"Wrote {path}.");
        log.Write(s.ToString());
        return 0;
    }
}
