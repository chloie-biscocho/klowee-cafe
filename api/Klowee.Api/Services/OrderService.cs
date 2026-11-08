using Klowee.Api.Common;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

/// <summary>
/// Office orders. The rules (decision 016):
///   - unit prices are copied from the menu version item when a line is first
///     created and never recomputed; a PUT carries an existing line's snapshot over;
///   - subtotal, discount and total are stored and recomputed on every change;
///   - an order with payments cannot be changed;
///   - a LoyaltyFree discount needs an available reward, and its redemption is a
///     ledger entry that lives and dies with the discount.
/// </summary>
public class OrderService : IOrderService
{
    private readonly KloweeDbContext _db;
    private readonly IPricingService _pricing;
    private readonly ILoyaltyService _loyalty;
    private readonly ISiteSettingService _settings;
    private readonly IMenuVersionService _menuVersions;

    public OrderService(
        KloweeDbContext db,
        IPricingService pricing,
        ILoyaltyService loyalty,
        ISiteSettingService settings,
        IMenuVersionService menuVersions)
    {
        _db = db;
        _pricing = pricing;
        _loyalty = loyalty;
        _settings = settings;
        _menuVersions = menuVersions;
    }

    public async Task<PagedResult<OrderSummaryDto>> ListAsync(OrderListQuery query, CancellationToken cancellationToken)
    {
        var orders = _db.Orders.AsNoTracking().AsQueryable();
        if (query.From is { } from)
        {
            orders = orders.Where(o => o.OrderedOn >= from);
        }

        if (query.To is { } to)
        {
            orders = orders.Where(o => o.OrderedOn <= to);
        }

        if (query.CustomerId is { } customerId)
        {
            // Orders the customer drank or paid for.
            orders = orders.Where(o => o.CustomerId == customerId || o.PayerId == customerId);
        }

        // Status is derived from payments, so it filters after loading; office
        // volumes (tens a month) make that free.
        var all = await SummariesAsync(orders, cancellationToken);
        var filtered = query.Status is { } status ? all.Where(o => o.Status == status).ToList() : all.ToList();

        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);
        return new PagedResult<OrderSummaryDto>(
            filtered.Skip((page - 1) * size).Take(size).ToList(), page, size, filtered.Count);
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> SummariesAsync(IQueryable<Order> orders, CancellationToken cancellationToken)
    {
        // Sorted after loading: the test suite's SQLite cannot ORDER BY a
        // DateTimeOffset (created_at), and the volumes make it free anyway.
        var loaded = await orders
            .Select(o => new
            {
                o.Id,
                o.OrderedOn,
                o.CreatedAt,
                Customer = new PersonRef(o.CustomerId, o.Customer!.Name),
                Payer = o.PayerId == null ? null : new PersonRef(o.PayerId.Value, o.Payer!.Name),
                Drinks = o.Items.Sum(i => i.Quantity),
                o.Total
            })
            .ToListAsync(cancellationToken);
        var rows = loaded.OrderByDescending(o => o.OrderedOn).ThenByDescending(o => o.CreatedAt).ToList();

        var orderIds = rows.Select(r => r.Id).ToList();
        var paid = await _db.PaidByOrderAsync(orderIds, cancellationToken);
        var items = await ItemsSummaryAsync(orderIds, cancellationToken);

        return rows.Select(r =>
        {
            var amount = paid.GetValueOrDefault(r.Id);
            return new OrderSummaryDto(r.Id, r.OrderedOn, r.Customer, r.Payer, r.Drinks,
                items.GetValueOrDefault(r.Id, string.Empty), r.Total, amount, SalesQueries.StatusFor(r.Total, amount));
        }).ToList();
    }

    /// <summary>"Spanish Latte ×2, Americano" per order, lines in their order, names looked up past soft-deletes.</summary>
    private async Task<Dictionary<Guid, string>> ItemsSummaryAsync(List<Guid> orderIds, CancellationToken cancellationToken)
    {
        var lines = await _db.OrderItems
            .AsNoTracking()
            .Where(i => orderIds.Contains(i.OrderId))
            .Select(i => new { i.OrderId, i.MenuVersionItemId, i.Quantity, i.SortOrder })
            .ToListAsync(cancellationToken);

        var versionItemIds = lines.Select(l => l.MenuVersionItemId).Distinct().ToList();
        var names = await _db.MenuVersionItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(vi => versionItemIds.Contains(vi.Id))
            .Select(vi => new { vi.Id, vi.MenuItem!.Name })
            .ToDictionaryAsync(vi => vi.Id, vi => vi.Name, cancellationToken);

        return lines
            .GroupBy(l => l.OrderId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.OrderBy(l => l.SortOrder).Select(l =>
                    names.GetValueOrDefault(l.MenuVersionItemId, "?") + (l.Quantity > 1 ? $" ×{l.Quantity}" : ""))));
    }

    public async Task<OrderDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Customer)
            .Include(o => o.Payer)
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Order", id);

        // Names come from the rows the lines point at, deleted or not: a draft
        // version's items are replaced on edit and a menu item can be retired,
        // but the order's history must still read.
        var versionItemIds = order.Items.Select(i => i.MenuVersionItemId).Distinct().ToList();
        var versionItems = await _db.MenuVersionItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(vi => versionItemIds.Contains(vi.Id))
            .Select(vi => new { vi.Id, vi.MenuItemId, vi.MenuVersionId, ItemName = vi.MenuItem!.Name, VersionName = vi.MenuVersion!.Name })
            .ToDictionaryAsync(vi => vi.Id, cancellationToken);

        var addOnIds = order.Items.SelectMany(i => i.AddOns).Select(a => a.AddOnId).Distinct().ToList();
        var addOnNames = await _db.AddOns
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => addOnIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        var currentOffice = await _menuVersions.FindCurrentIdAsync(MenuContext.Office, cancellationToken);
        var warnings = new List<string>();

        var items = order.Items
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Select(i =>
            {
                var source = versionItems[i.MenuVersionItemId];
                if (source.MenuVersionId != currentOffice)
                {
                    warnings.Add(currentOffice is null
                        ? $"{source.ItemName} is from \"{source.VersionName}\"; there is no current office menu."
                        : $"{source.ItemName} is from \"{source.VersionName}\", not the current office menu.");
                }

                return new OrderItemDto(
                    i.Id, i.MenuVersionItemId, source.MenuItemId, source.ItemName, source.VersionName,
                    i.Quantity, i.UnitPrice, i.LineTotal,
                    i.AddOns.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
                        .Select(a => new OrderItemAddOnDto(a.Id, a.AddOnId, addOnNames[a.AddOnId], a.UnitPrice))
                        .ToList());
            })
            .ToList();

        var paid = order.Payments.Sum(p => p.Amount);
        return new OrderDetailDto(
            order.Id, order.Context, order.OrderedOn,
            new PersonRef(order.CustomerId, order.Customer!.Name),
            order.Payer is null ? null : new PersonRef(order.Payer.Id, order.Payer.Name),
            order.Notes, order.DiscountType, order.DiscountValue,
            order.Subtotal, order.DiscountAmount, order.Total,
            paid, order.Total - paid, SalesQueries.StatusFor(order.Total, paid),
            items,
            order.Payments.OrderBy(p => p.PaidOn).ThenBy(p => p.CreatedAt)
                .Select(p => new PaymentDto(p.Id, p.OrderId, p.Amount, p.Channel, p.PaidOn, p.Reference, p.Notes))
                .ToList(),
            warnings);
    }

    public async Task<OrderDetailDto> CreateAsync(OrderRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var order = new Order();
        await ApplyAsync(order, request, previousLines: [], cancellationToken);
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        await _loyalty.ReconcileAsync(order, paid: 0m, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(order.Id, cancellationToken);
    }

    public async Task<OrderDetailDto> UpdateAsync(Guid id, OrderRequest request, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(id, cancellationToken);

        if (await _db.Payments.AnyAsync(p => p.OrderId == id, cancellationToken))
        {
            throw new ConflictException(
                "This order has payments, so its items and discount can't change. Remove the payments first.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        // The old lines are retired; their snapshots are offered to the new ones.
        var previous = order.Items.ToList();
        var now = DateTimeOffset.UtcNow;
        foreach (var item in previous)
        {
            item.DeletedAt = now;
            foreach (var addOn in item.AddOns)
            {
                addOn.DeletedAt = now;
            }
        }

        // The new lines join the same collection. Swapping in a new list would
        // orphan the old lines, and EF hard-deletes orphans of a required key.
        await ApplyAsync(order, request, previous, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        await _loyalty.ReconcileAsync(order, paid: 0m, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(order.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await LoadAsync(id, cancellationToken);
        var payments = await _db.Payments.Where(p => p.OrderId == id).ToListAsync(cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        order.DeletedAt = now;
        foreach (var item in order.Items)
        {
            item.DeletedAt = now;
            foreach (var addOn in item.AddOns)
            {
                addOn.DeletedAt = now;
            }
        }

        foreach (var payment in payments)
        {
            payment.DeletedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _loyalty.ReconcileAsync(order, paid: 0m, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Validates the request and writes it onto <paramref name="order"/>: people,
    /// lines with their price snapshots, and the stored totals.
    /// </summary>
    private async Task ApplyAsync(
        Order order,
        OrderRequest request,
        List<OrderItem> previousLines,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            throw new ValidationFailedException("An order needs at least one item.");
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new ValidationFailedException($"Customer '{request.CustomerId}' was not found.");

        // Re-saving an order for someone who has since been deactivated is
        // fine; starting a new tab for them is not.
        if (!customer.IsActive && order.CustomerId != customer.Id)
        {
            throw new ValidationFailedException($"{customer.Name} is inactive. Activate them before adding orders.");
        }

        // Paying for yourself is just paying.
        var payerId = request.PayerId == request.CustomerId ? null : request.PayerId;
        if (payerId is { } payer && !await _db.Customers.AnyAsync(c => c.Id == payer, cancellationToken))
        {
            throw new ValidationFailedException($"Payer '{payer}' was not found.");
        }

        var versionItemIds = request.Items.Select(i => i.MenuVersionItemId).Distinct().ToList();
        var prices = await _db.MenuVersionItems
            .Where(vi => versionItemIds.Contains(vi.Id))
            .ToDictionaryAsync(vi => vi.Id, vi => vi.Price, cancellationToken);
        var missing = versionItemIds.Where(id => !prices.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new ValidationFailedException($"Menu version item(s) not found: {string.Join(", ", missing)}.");
        }

        var addOnIds = request.Items.SelectMany(i => i.AddOnIds).Distinct().ToList();
        var addOnPrices = await _db.AddOns
            .Where(a => addOnIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Price, cancellationToken);
        var missingAddOns = addOnIds.Where(id => !addOnPrices.ContainsKey(id)).ToList();
        if (missingAddOns.Count > 0)
        {
            throw new ValidationFailedException($"Add-on(s) not found: {string.Join(", ", missingAddOns)}.");
        }

        // A line for a drink that was already on the order keeps the price it
        // was sold at; so does each add-on already on that line.
        var unused = previousLines.ToList();
        var lines = new List<OrderItem>();
        foreach (var line in request.Items)
        {
            var before = unused.FirstOrDefault(p => p.MenuVersionItemId == line.MenuVersionItemId);
            if (before is not null)
            {
                unused.Remove(before);
            }

            var item = new OrderItem
            {
                MenuVersionItemId = line.MenuVersionItemId,
                Quantity = line.Quantity,
                SortOrder = lines.Count,
                UnitPrice = before?.UnitPrice ?? prices[line.MenuVersionItemId]
            };

            foreach (var addOnId in line.AddOnIds)
            {
                var addOnBefore = before?.AddOns.FirstOrDefault(a => a.AddOnId == addOnId);
                item.AddOns.Add(new OrderItemAddOn
                {
                    AddOnId = addOnId,
                    UnitPrice = addOnBefore?.UnitPrice ?? addOnPrices[addOnId]
                });
            }

            lines.Add(item);
        }

        var pricingLines = lines
            .Select(l => new PricingLine(l.UnitPrice, l.Quantity, l.AddOns.Select(a => a.UnitPrice).ToList()))
            .ToList();

        var discountValue = request.DiscountType is DiscountType.Percent or DiscountType.Fixed ? request.DiscountValue : 0m;
        if (request.DiscountType == DiscountType.Percent && discountValue is < 0m or > 100m)
        {
            throw new ValidationFailedException("A percent discount must be from 0 to 100.");
        }

        if (request.DiscountType == DiscountType.Fixed)
        {
            var subtotal = _pricing.Price(pricingLines, DiscountType.None, 0m).Subtotal;
            if (discountValue < 0m || discountValue > subtotal)
            {
                throw new ValidationFailedException($"A fixed discount must be from 0 to the subtotal (₱{subtotal:0.00}).");
            }
        }

        if (request.DiscountType == DiscountType.LoyaltyFree)
        {
            // This order's own redemption doesn't count against it, so re-saving
            // an order that already uses a reward still works.
            var excluding = order.Id == Guid.Empty || order.CustomerId != request.CustomerId ? (Guid?)null : order.Id;
            var balance = await _loyalty.BalanceAsync(request.CustomerId, excluding, cancellationToken);
            var perReward = await _settings.GetLoyaltyStampsPerRewardAsync(cancellationToken);
            if (balance / perReward < 1)
            {
                throw new ConflictException(
                    $"{customer.Name} has {balance} stamp(s); a free drink needs {perReward}.");
            }
        }

        var priced = _pricing.Price(pricingLines, request.DiscountType, discountValue);
        for (var i = 0; i < lines.Count; i++)
        {
            lines[i].LineTotal = priced.LineTotals[i];
            order.Items.Add(lines[i]);
        }

        order.CustomerId = request.CustomerId;
        order.PayerId = payerId;
        order.Context = OrderContext.Office;
        order.OrderedOn = request.OrderedOn;
        order.Notes = request.Notes?.Trim();
        order.DiscountType = request.DiscountType;
        order.DiscountValue = discountValue;
        order.Subtotal = priced.Subtotal;
        order.DiscountAmount = priced.DiscountAmount;
        order.Total = priced.Total;
    }

    private async Task<Order> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
        ?? throw NotFoundException.For("Order", id);
}
