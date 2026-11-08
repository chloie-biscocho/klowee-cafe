using Klowee.Api.Common;
using Klowee.Api.Contracts.Reports;
using Klowee.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

/// <summary>
/// Read-only figures for the dashboard. Revenue follows ordered_on, collected
/// follows paid_on: a June order paid in July is June revenue and July
/// collection. Everything is summed in memory (see <see cref="SalesQueries"/>).
/// </summary>
public class ReportService : IReportService
{
    private readonly KloweeDbContext _db;
    private readonly IClock _clock;

    public ReportService(KloweeDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<SummaryReportDto> SummaryAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var today = _clock.Today;
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        if (start > end)
        {
            throw new ValidationFailedException("'from' must be on or before 'to'.");
        }

        var orders = await LoadOrdersAsync(cancellationToken);
        var payments = await LoadPaymentsAsync(cancellationToken);
        var paidByOrder = payments.GroupBy(p => p.OrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var inRange = orders.Where(o => o.OrderedOn >= start && o.OrderedOn <= end).ToList();
        var paymentsInRange = payments.Where(p => p.PaidOn >= start && p.PaidOn <= end).ToList();

        var lines = inRange.SelectMany(o => o.Lines).ToList();
        var itemNames = await _db.MenuItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(m => new { m.Id, m.Name })
            .ToDictionaryAsync(m => m.Id, m => m.Name, cancellationToken);

        var firstOrders = orders.GroupBy(o => o.CustomerId).Select(g => g.Min(o => o.OrderedOn));

        return new SummaryReportDto(
            start,
            end,
            Revenue: inRange.Sum(o => o.Total),
            Collected: paymentsInRange.Sum(p => p.Amount),
            Outstanding: orders.Sum(o => o.Total - paidByOrder.GetValueOrDefault(o.Id)),
            OrderCount: inRange.Count,
            DrinkCount: lines.Sum(l => l.Quantity),
            ByChannel: paymentsInRange
                .GroupBy(p => p.Channel)
                .Select(g => new ChannelTotalDto(g.Key, g.Sum(p => p.Amount), g.Count()))
                .OrderByDescending(c => c.Amount)
                .ToList(),
            TopItems: lines
                .GroupBy(l => l.MenuItemId)
                .Select(g => new TopItemDto(g.Key, itemNames.GetValueOrDefault(g.Key, "(unknown)"), g.Sum(l => l.Quantity)))
                .OrderByDescending(t => t.Quantity)
                .ThenBy(t => t.Name)
                .Take(10)
                .ToList(),
            NewCustomers: firstOrders.Count(first => first >= start && first <= end));
    }

    public async Task<IReadOnlyList<OutstandingRowDto>> OutstandingAsync(CancellationToken cancellationToken)
    {
        var orders = await LoadOrdersAsync(cancellationToken);
        var payments = await LoadPaymentsAsync(cancellationToken);
        var paidByOrder = payments.GroupBy(p => p.OrderId).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var open = orders
            .Select(o => (Order: o, Owed: o.Total - paidByOrder.GetValueOrDefault(o.Id)))
            .Where(x => x.Owed > 0m)
            .ToList();

        var tabOwners = open.Select(x => x.Order.TabOwnerId).Distinct().ToList();
        var names = await _db.Customers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => tabOwners.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        return open
            .GroupBy(x => x.Order.TabOwnerId)
            .Select(g => new OutstandingRowDto(
                g.Key,
                names.GetValueOrDefault(g.Key, "(unknown)"),
                g.Sum(x => x.Owed),
                g.Min(x => x.Order.OrderedOn),
                g.Count()))
            .OrderByDescending(r => r.Balance)
            .ThenBy(r => r.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<MonthlyRowDto>> MonthlyAsync(int? year, CancellationToken cancellationToken)
    {
        var y = year ?? _clock.Today.Year;
        if (y is < 2000 or > 2100)
        {
            throw new ValidationFailedException("'year' must be between 2000 and 2100.");
        }

        return await MonthlyRangeAsync(new DateOnly(y, 1, 1), new DateOnly(y, 12, 1), cancellationToken);
    }

    public const int MaxMonths = 240;

    public async Task<IReadOnlyList<MonthlyRowDto>> MonthlyRangeAsync(DateOnly? from, DateOnly to, CancellationToken cancellationToken)
    {
        var orders = await LoadOrdersAsync(cancellationToken);
        var payments = await LoadPaymentsAsync(cancellationToken);

        // "All": from the first month with an order (or just `to` when there are none).
        var first = from ?? (orders.Count > 0 ? orders.Min(o => o.OrderedOn) : to);
        var start = new DateOnly(first.Year, first.Month, 1);
        var end = new DateOnly(to.Year, to.Month, 1);
        if (start > end)
        {
            throw new ValidationFailedException("'from' must not be after 'to'.");
        }

        var count = (end.Year - start.Year) * 12 + end.Month - start.Month + 1;
        if (count > MaxMonths)
        {
            throw new ValidationFailedException($"A range can cover at most {MaxMonths} months.");
        }

        static int Key(DateOnly d) => d.Year * 12 + d.Month - 1;
        var ordersByMonth = orders.ToLookup(o => Key(o.OrderedOn));
        var paidByMonth = payments.ToLookup(p => Key(p.PaidOn));

        return Enumerable.Range(0, count)
            .Select(i =>
            {
                var month = start.AddMonths(i);
                var monthOrders = ordersByMonth[Key(month)].ToList();
                return new MonthlyRowDto(
                    month.Year,
                    month.Month,
                    monthOrders.Sum(o => o.Total),
                    paidByMonth[Key(month)].Sum(p => p.Amount),
                    monthOrders.Count,
                    monthOrders.SelectMany(o => o.Lines).Sum(l => l.Quantity));
            })
            .ToList();
    }

    private sealed record ReportLine(Guid MenuItemId, int Quantity);

    private sealed record ReportOrder(Guid Id, Guid CustomerId, Guid TabOwnerId, DateOnly OrderedOn, decimal Total, List<ReportLine> Lines);

    private sealed record ReportPayment(Guid OrderId, decimal Amount, Entities.PaymentChannel Channel, DateOnly PaidOn);

    private async Task<List<ReportOrder>> LoadOrdersAsync(CancellationToken cancellationToken)
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .Select(o => new
            {
                o.Id,
                o.CustomerId,
                o.PayerId,
                o.OrderedOn,
                o.Total,
                // The menu item behind each line, even if the version row was
                // later replaced: the lookup ignores the soft-delete filter.
                Lines = o.Items.Select(i => new { i.MenuVersionItemId, i.Quantity }).ToList()
            })
            .ToListAsync(cancellationToken);

        var versionItemIds = orders.SelectMany(o => o.Lines).Select(l => l.MenuVersionItemId).Distinct().ToList();
        var menuItemOf = await _db.MenuVersionItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(vi => versionItemIds.Contains(vi.Id))
            .ToDictionaryAsync(vi => vi.Id, vi => vi.MenuItemId, cancellationToken);

        return orders
            .Select(o => new ReportOrder(o.Id, o.CustomerId, o.PayerId ?? o.CustomerId, o.OrderedOn, o.Total,
                o.Lines.Select(l => new ReportLine(menuItemOf[l.MenuVersionItemId], l.Quantity)).ToList()))
            .ToList();
    }

    private async Task<List<ReportPayment>> LoadPaymentsAsync(CancellationToken cancellationToken) =>
        await _db.Payments
            .AsNoTracking()
            .Select(p => new ReportPayment(p.OrderId, p.Amount, p.Channel, p.PaidOn))
            .ToListAsync(cancellationToken);
}
