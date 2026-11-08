using System.Text;
using System.Text.RegularExpressions;

namespace Klowee.SheetImport;

/// <summary>An order already in the environment: imported (it carries a marker) or entered in the admin.</summary>
public sealed record ExistingOrder(Guid Id, DateOnly OrderedOn, string? Marker, decimal Total, decimal Paid, string CustomerName);

/// <summary>Reads what the environment already holds and writes the plan through the API.</summary>
public sealed partial class Importer(Api api, TextWriter log)
{
    public const string RetiredCategory = "Retired drinks";

    /// <summary>Every order in the sheet's date range, with its import marker if it has one (the marker lives in the notes, so each order's detail is read).</summary>
    public async Task<List<ExistingOrder>> ExistingOrdersAsync(DateOnly from, DateOnly to)
    {
        var summaries = new List<OrderSummary>();
        for (var page = 1; ; page++)
        {
            var result = await api.OrdersAsync(from, to, page);
            summaries.AddRange(result.Items);
            if (summaries.Count >= result.TotalCount || result.Items.Count == 0) break;
        }

        var found = new ExistingOrder[summaries.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, summaries.Count), new ParallelOptions { MaxDegreeOfParallelism = 6 }, async (i, _) =>
        {
            var s = summaries[i];
            var detail = await api.OrderAsync(s.Id);
            var marker = MarkerPattern().Match(detail.Notes ?? "");
            found[i] = new ExistingOrder(s.Id, s.OrderedOn, marker.Success ? marker.Value : null, detail.Total, detail.Paid, s.Customer.Name);
        });
        return [.. found];
    }

    public async Task ApplyAsync(ImportPlan plan, List<ExistingOrder> existing)
    {
        // ---- Menu: retired drinks, then the historical versions ----
        var items = _items = (await api.ItemsAsync()).ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
        var missing = plan.Items.Where(i => !items.ContainsKey(i)).ToList();
        if (missing.Count > 0)
        {
            var categories = await api.CategoriesAsync();
            var retired = categories.FirstOrDefault(c => c.Name.Equals(RetiredCategory, StringComparison.OrdinalIgnoreCase))
                ?? await api.CreateCategoryAsync(RetiredCategory, categories.Max(c => c.SortOrder) + 1);
            foreach (var name in missing)
            {
                items[name] = await api.CreateItemAsync(name, "No longer sold; created by the spreadsheet import for history.", retired.Id);
                log.WriteLine($"  item     created \"{name}\" (archived once the versions are built)");
            }
        }

        // Archived drinks can't be priced into a version, so the ones a version needs are unarchived for a moment.
        var retiredIds = plan.Items.Where(i => items[i].IsArchived || missing.Contains(i)).Select(i => items[i].Id).ToHashSet();
        var versions = (await api.VersionsAsync()).Where(v => v.Context == "Office").ToDictionary(v => v.Name);
        var versionItems = new Dictionary<(string Version, string Item), Guid>();
        var unarchived = new HashSet<Guid>();
        try
        {
            foreach (var planned in plan.Versions)
            {
                var lines = planned.Prices.OrderBy(p => p.Key)
                    .Select((p, i) => new PricedLine(items[p.Key].Id, p.Value, true, false, i)).ToList();
                VersionDetail detail;
                if (!versions.TryGetValue(planned.Name, out var summary))
                {
                    detail = await api.CreateVersionAsync(planned.Name, "Office", planned.EffectiveFrom);
                    log.WriteLine($"  version  created \"{planned.Name}\" (unpublished)");
                }
                else
                {
                    detail = await api.VersionAsync(summary.Id);
                }

                if (detail.Items.Count == 0)
                {
                    foreach (var line in lines.Where(l => retiredIds.Contains(l.MenuItemId) && !unarchived.Contains(l.MenuItemId)))
                    {
                        await api.SetArchivedAsync(line.MenuItemId, false);
                        unarchived.Add(line.MenuItemId);
                    }

                    detail = await api.ReplaceItemsAsync(detail.Id, lines);
                    log.WriteLine($"  version  \"{planned.Name}\": {lines.Count} drinks priced");
                }
                else if (!Same(detail, lines))
                {
                    log.WriteLine($"  note     \"{planned.Name}\" already has different items; left alone, orders use its prices");
                }

                foreach (var vi in detail.Items)
                    versionItems[(planned.Name, items.Values.First(i => i.Id == vi.MenuItemId).Name)] = vi.Id;
            }
        }
        finally
        {
            foreach (var id in retiredIds.Where(id => unarchived.Contains(id) || missing.Any(m => items[m].Id == id)))
                await api.SetArchivedAsync(id, true);
        }

        // ---- Customers ----
        var people = await CustomerIdsAsync();
        foreach (var name in plan.Customers.Where(c => !people.ContainsKey(c)))
        {
            people[name] = (await api.CreateCustomerAsync(name)).Id;
            log.WriteLine($"  customer created \"{name}\"");
        }

        // ---- Orders and payments, in date order so stamps replay ----
        var addOns = await api.AddOnsAsync();
        var shot = addOns.Single(a => a.Name.Equals("Extra shot", StringComparison.OrdinalIgnoreCase)).Id;
        var oat = addOns.Single(a => a.Name.Equals("Oat milk", StringComparison.OrdinalIgnoreCase)).Id;
        var done = existing.Where(e => e.Marker is not null).ToDictionary(e => e.Marker!);
        int created = 0, paidCount = 0, mismatched = 0;

        foreach (var order in plan.Orders)
        {
            if (done.TryGetValue(order.Marker, out var already))
            {
                if (order.Pay && already.Paid == 0)
                {
                    await PayAsync(order, already.Id, already.Total);
                    paidCount++;
                }

                continue;
            }

            var type = order.DiscountType;
            var value = order.DiscountValue;
            if (order.Row.Discount == "Free")
            {
                // The replay predicted the reward; the API's count is the truth.
                var rewards = (await api.CustomerAsync(people[order.Customer])).AvailableRewards;
                if (type == "LoyaltyFree" && rewards < 1)
                {
                    var unit = order.Version.Prices[order.Item] + (order.Shot ? addOns.First(a => a.Id == shot).Price : 0) + (order.Oat ? addOns.First(a => a.Id == oat).Price : 0);
                    (type, value) = ("Fixed", unit * order.Quantity - order.Row.Total);
                    log.WriteLine($"  note     {order.Marker}: no reward on the API after all; ₱{Planner.Money(value)} off instead");
                }
            }

            var addOnIds = new List<Guid>();
            if (order.Shot) addOnIds.Add(shot);
            if (order.Oat) addOnIds.Add(oat);
            var detail = await api.CreateOrderAsync(new NewOrder(
                people[order.Customer], order.Payer is null ? null : people[order.Payer], order.Row.Date, order.Notes, type, value,
                [new OrderLine(VersionItem(versionItems, order), order.Quantity, addOnIds)]));
            created++;
            if (detail.Total != order.Total)
            {
                mismatched++;
                log.WriteLine($"  note     {order.Marker}: API total ₱{Planner.Money(detail.Total)}, planned ₱{Planner.Money(order.Total)}");
            }

            if (order.Pay && detail.Total > 0)
            {
                await PayAsync(order, detail.Id, detail.Total);
                paidCount++;
            }

            if (created % 50 == 0) log.WriteLine($"  progress {created} orders created");
        }

        log.WriteLine($"Applied: {created} orders created, {paidCount} payments, {mismatched} totals differing from the plan.");
    }

    public async Task RollbackAsync(List<ExistingOrder> imported)
    {
        var count = 0;
        foreach (var order in imported)
        {
            await api.DeleteOrderAsync(order.Id);
            if (++count % 50 == 0) log.WriteLine($"  progress {count} orders deleted");
        }

        log.WriteLine($"Rolled back: {count} imported orders deleted (soft delete, payments and stamps with them).");
    }

    public async Task<Dictionary<string, Guid>> CustomerIdsAsync()
    {
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in await api.CustomersAsync())
        {
            map.TryAdd(c.Name.Trim(), c.Id);
            if (!string.IsNullOrWhiteSpace(c.Nickname)) map.TryAdd(c.Nickname.Trim(), c.Id);
        }

        return map;
    }

    private Task PayAsync(PlannedOrder order, Guid orderId, decimal amount) =>
        api.AddPaymentAsync(orderId, new NewPayment(amount, order.Channel, order.PaidOn, order.PaymentNotes));

    private Dictionary<string, Item> _items = new(StringComparer.OrdinalIgnoreCase);

    private Guid VersionItem(Dictionary<(string Version, string Item), Guid> versionItems, PlannedOrder order) =>
        versionItems.TryGetValue((order.Version.Name, _items[order.Item].Name), out var id)
            ? id
            : throw new ImportException($"{order.Marker}: \"{order.Version.Name}\" has no {order.Item}. Its items differ from the plan; check it in the admin.");

    private static bool Same(VersionDetail detail, List<PricedLine> lines) =>
        detail.Items.Count == lines.Count && lines.All(l => detail.Items.Any(i => i.MenuItemId == l.MenuItemId && i.Price == l.Price));

    [GeneratedRegex(@"^\[import:[^\]]+:\d+\]")]
    private static partial Regex MarkerPattern();
}
