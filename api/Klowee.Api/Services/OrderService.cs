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
///   - a line may carry one loyalty reward (decision 020) for one unit: it must
///     be available to the drinker on the drink's club when the order is saved,
///     and its redemption lives and dies with the line;
///   - LoyaltyFree, the old single card's order discount, is history only.
/// </summary>
public class OrderService : IOrderService
{
    private readonly KloweeDbContext _db;
    private readonly IPricingService _pricing;
    private readonly ILoyaltyService _loyalty;
    private readonly IMenuVersionService _menuVersions;

    public OrderService(
        KloweeDbContext db,
        IPricingService pricing,
        ILoyaltyService loyalty,
        IMenuVersionService menuVersions)
    {
        _db = db;
        _pricing = pricing;
        _loyalty = loyalty;
        _menuVersions = menuVersions;
    }

    /// <summary>A reward a saved line will carry: a redemption kept from before, or a new one for this cycle.</summary>
    private sealed record PlannedReward(OrderItem Line, LoyaltyClubReward Reward, LoyaltyRedemption? Kept, int Cycle);

    /// <summary>What saving the order does to its redemptions.</summary>
    private sealed record RewardPlan(List<PlannedReward> Rewards, List<LoyaltyRedemption> Released);

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

        // A line's reward reads past soft-deletes: the club's rewards may have been edited since.
        var redemptionIds = order.Items.Where(i => i.LoyaltyRedemptionId != null).Select(i => i.LoyaltyRedemptionId!.Value).ToList();
        var lineRewards = await _db.LoyaltyRedemptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => redemptionIds.Contains(r.Id))
            .Select(r => new { r.Id, r.RewardId, r.Reward!.Label, r.Reward.Type })
            .ToDictionaryAsync(r => r.Id, cancellationToken);

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

                var reward = i.LoyaltyRedemptionId is { } rid && lineRewards.TryGetValue(rid, out var r)
                    ? new OrderLineRewardDto(r.RewardId, r.Label, r.Type,
                        (i.UnitPrice + i.AddOns.Sum(a => a.UnitPrice)) * i.Quantity - i.LineTotal)
                    : null;

                return new OrderItemDto(
                    i.Id, i.MenuVersionItemId, source.MenuItemId, source.ItemName, source.VersionName,
                    i.Quantity, i.UnitPrice, i.LineTotal,
                    i.AddOns.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
                        .Select(a => new OrderItemAddOnDto(a.Id, a.AddOnId, addOnNames[a.AddOnId], a.UnitPrice))
                        .ToList(),
                    reward);
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
        var rewards = await ApplyAsync(order, request, previousLines: [], cancellationToken);
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        await SaveRewardsAsync(order, rewards, cancellationToken);
        await _loyalty.ReconcileAsync(order, paid: 0m, linesChanged: true, cancellationToken);
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
        var rewards = await ApplyAsync(order, request, previous, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        await SaveRewardsAsync(order, rewards, cancellationToken);
        await _loyalty.ReconcileAsync(order, paid: 0m, linesChanged: true, cancellationToken);
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

        // Its rewards become available again.
        foreach (var redemption in await _db.LoyaltyRedemptions.Where(r => r.OrderId == id).ToListAsync(cancellationToken))
        {
            redemption.DeletedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _loyalty.ReconcileAsync(order, paid: 0m, linesChanged: true, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Validates the request and writes it onto <paramref name="order"/>: people,
    /// lines with their price snapshots and rewards, and the stored totals.
    /// Returns what has to happen to the order's redemptions once the lines are saved.
    /// </summary>
    private async Task<RewardPlan> ApplyAsync(
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

        if (request.DiscountType == DiscountType.LoyaltyFree)
        {
            throw new ValidationFailedException(
                "The old free-drink discount is no longer used. Put the loyalty reward on the drink's line instead.");
        }

        var versionItemIds = request.Items.Select(i => i.MenuVersionItemId).Distinct().ToList();
        var priced = await _db.MenuVersionItems
            .Where(vi => versionItemIds.Contains(vi.Id))
            .Select(vi => new { vi.Id, vi.Price, vi.MenuItem!.Name })
            .ToDictionaryAsync(vi => vi.Id, cancellationToken);
        var prices = priced.ToDictionary(p => p.Key, p => p.Value.Price);
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

        var plan = await PlanRewardsAsync(order, request, customer, lines, previousLines,
            id => priced[id].Name, cancellationToken);
        var rewardOf = plan.Rewards.ToDictionary(r => r.Line, r => r.Reward);

        var pricingLines = lines
            .Select(l => new PricingLine(
                l.UnitPrice, l.Quantity, l.AddOns.Select(a => a.UnitPrice).ToList(),
                rewardOf.TryGetValue(l, out var reward) ? PricingRewardFor(reward, l) : null))
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

        var money = _pricing.Price(pricingLines, request.DiscountType, discountValue);
        for (var i = 0; i < lines.Count; i++)
        {
            lines[i].LineTotal = money.LineTotals[i];
            order.Items.Add(lines[i]);
        }

        order.CustomerId = request.CustomerId;
        order.PayerId = payerId;
        order.Context = OrderContext.Office;
        order.OrderedOn = request.OrderedOn;
        order.Notes = request.Notes?.Trim();
        order.DiscountType = request.DiscountType;
        order.DiscountValue = discountValue;
        order.Subtotal = money.Subtotal;
        order.DiscountAmount = money.DiscountAmount;
        order.Total = money.Total;

        return plan;
    }

    /// <summary>
    /// Rule 3: which reward each line carries, checked against the drinker's
    /// cards. A line that keeps its drink, drinker and reward keeps its
    /// redemption; every other redemption of the order is released, and the
    /// rest are allocated from what is available, oldest card first, as if the
    /// released ones were already gone.
    /// </summary>
    private async Task<RewardPlan> PlanRewardsAsync(
        Order order,
        OrderRequest request,
        Customer customer,
        List<OrderItem> lines,
        List<OrderItem> previousLines,
        Func<Guid, string> drinkName,
        CancellationToken cancellationToken)
    {
        var existing = order.Id == Guid.Empty
            ? []
            : await _db.LoyaltyRedemptions.Where(r => r.OrderId == order.Id).ToListAsync(cancellationToken);

        var wanted = request.Items.Select((item, i) => (Line: lines[i], item.RewardId, Before: BeforeOf(i)))
            .Where(w => w.RewardId is not null)
            .ToList();
        if (wanted.Count == 0)
        {
            return new RewardPlan([], existing);
        }

        var rewardIds = wanted.Select(w => w.RewardId!.Value).Distinct().ToList();
        var rewards = await _db.LoyaltyClubRewards
            .Include(r => r.Club)
            .Where(r => rewardIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);
        var unknown = rewardIds.Where(id => !rewards.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationFailedException($"Reward(s) not found: {string.Join(", ", unknown)}.");
        }

        var clubs = await LoyaltyClubMap.LoadAsync(_db, lines.Select(l => l.MenuVersionItemId).Distinct().ToList(), cancellationToken);
        var cards = await _db.LoyaltyMemberships
            .Where(m => m.CustomerId == request.CustomerId && m.Status == MembershipStatus.Active)
            .Select(m => m.ClubId)
            .ToListAsync(cancellationToken);
        var planned = new List<PlannedReward>();
        var kept = new HashSet<Guid>();
        var toAllocate = new List<(OrderItem Line, LoyaltyClubReward Reward)>();

        foreach (var (line, rewardId, before) in wanted)
        {
            var reward = rewards[rewardId!.Value];
            var club = reward.Club!;
            var name = drinkName(line.MenuVersionItemId);

            if (clubs.ForVersionItem(line.MenuVersionItemId) != club.Id)
            {
                throw new ConflictException($"{reward.Label} is a {club.Name} reward, and {name} doesn't stamp that card.");
            }

            if (!club.IsActive)
            {
                throw new ConflictException($"{club.Name} isn't active, so its rewards can't be used.");
            }

            if (!cards.Contains(club.Id))
            {
                throw new ConflictException($"{customer.Name} has no {club.Name} card, so its rewards can't be used.");
            }

            if (reward.Type == LoyaltyRewardType.FreeAddOn && reward.AddOnId is null)
            {
                throw new ConflictException($"{reward.Label} has no add-on set. Choose it under Menu → Loyalty cards.");
            }

            var same = before?.LoyaltyRedemptionId is { } previous
                ? existing.FirstOrDefault(r => r.Id == previous && r.RewardId == reward.Id && r.CustomerId == request.CustomerId)
                : null;
            if (same is not null && kept.Add(same.Id))
            {
                planned.Add(new PlannedReward(line, reward, same, same.Cycle));
            }
            else
            {
                toAllocate.Add((line, reward));
            }
        }

        var released = existing.Where(r => !kept.Contains(r.Id)).ToList();
        foreach (var group in toAllocate.GroupBy(a => a.Reward.ClubId))
        {
            var club = group.First().Reward.Club!;
            var card = await _loyalty.CardAsync(request.CustomerId, club, released.Select(r => r.Id).ToList(), cancellationToken);
            var vouchers = card.Available.ToList();

            foreach (var (line, reward) in group)
            {
                var voucher = vouchers.FirstOrDefault(v => v.RewardId == reward.Id)
                    ?? throw new ConflictException(
                        $"{customer.Name} has no {reward.Label} reward available on {club.Name} " +
                        $"({card.Position} of {club.SlotCount} slots filled).");
                vouchers.Remove(voucher);
                planned.Add(new PlannedReward(line, reward, null, voucher.Cycle));
            }
        }

        return new RewardPlan(planned, released);

        // The previous line this one takes its snapshot from (same matching as the prices above).
        OrderItem? BeforeOf(int index)
        {
            var unused = previousLines.ToList();
            for (var i = 0; i <= index; i++)
            {
                var match = unused.FirstOrDefault(p => p.MenuVersionItemId == request.Items[i].MenuVersionItemId);
                if (match is not null)
                {
                    unused.Remove(match);
                }

                if (i == index)
                {
                    return match;
                }
            }

            return null;
        }
    }

    /// <summary>The pricing input for a line's reward.</summary>
    private static PricingReward PricingRewardFor(LoyaltyClubReward reward, OrderItem line) => new(
        reward.Type,
        reward.Percent ?? 0m,
        reward.Type == LoyaltyRewardType.FreeAddOn
            ? line.AddOns.FirstOrDefault(a => a.AddOnId == reward.AddOnId)?.UnitPrice
            : null);

    /// <summary>
    /// After the lines are saved: releases the redemptions no line keeps, then
    /// ties each line to its redemption, kept or new. Two saves, because the
    /// unique (customer, club, cycle, reward) index must see the old row gone
    /// before a new one for the same cycle arrives.
    /// </summary>
    private async Task SaveRewardsAsync(Order order, RewardPlan plan, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var released in plan.Released)
        {
            released.DeletedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var (line, reward, kept, cycle) in plan.Rewards)
        {
            var redemption = kept ?? new LoyaltyRedemption
            {
                CustomerId = order.CustomerId,
                ClubId = reward.ClubId,
                RewardId = reward.Id,
                Cycle = cycle,
                OrderId = order.Id
            };

            redemption.OrderItemId = line.Id;
            if (kept is null)
            {
                _db.LoyaltyRedemptions.Add(redemption);
            }

            line.LoyaltyRedemption = redemption;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Order> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Orders
            .Include(o => o.Items).ThenInclude(i => i.AddOns)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
        ?? throw NotFoundException.For("Order", id);
}
