using Klowee.Api.Common;
using Klowee.Api.Contracts.Customers;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

/// <summary>
/// Office customers. A customer's outstanding balance is their tab: the
/// orders they pay for (their own unless someone else pays, plus any they
/// cover), minus payments. Loyalty belongs to the drinker.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly KloweeDbContext _db;
    private readonly IOrderService _orders;
    private readonly ISiteSettingService _settings;

    public CustomerService(KloweeDbContext db, IOrderService orders, ISiteSettingService settings)
    {
        _db = db;
        _orders = orders;
        _settings = settings;
    }

    public async Task<IReadOnlyList<CustomerSummaryDto>> ListAsync(
        bool includeInactive,
        string? search,
        CancellationToken cancellationToken)
    {
        var customers = _db.Customers.AsNoTracking().Where(c => includeInactive || c.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            customers = customers.Where(c => c.Name.ToLower().Contains(term)
                || (c.Nickname != null && c.Nickname.ToLower().Contains(term)));
        }

        var list = await customers.OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var figures = await FiguresAsync(list.Select(c => c.Id).ToList(), cancellationToken);

        return list.Select(c => Summary(c, figures)).ToList();
    }

    public async Task<CustomerDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Customer", id);

        var figures = await FiguresAsync([id], cancellationToken);
        var summary = Summary(customer, figures);
        var recent = await _orders.SummariesAsync(
            _db.Orders.AsNoTracking().Where(o => o.CustomerId == id || o.PayerId == id),
            cancellationToken);

        return new CustomerDetailDto(
            summary.Id, summary.Name, summary.Nickname, summary.Notes, summary.IsActive,
            summary.OutstandingBalance, summary.LoyaltyBalance, summary.AvailableRewards, summary.LastOrderOn,
            recent.Take(20).ToList());
    }

    public async Task<CustomerDetailDto> CreateAsync(CustomerRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludingId: null, cancellationToken);

        var customer = new Customer
        {
            Name = name,
            Nickname = request.Nickname?.Trim(),
            Notes = request.Notes?.Trim(),
            IsActive = request.IsActive
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(customer.Id, cancellationToken);
    }

    public async Task<CustomerDetailDto> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await LoadAsync(id, cancellationToken);
        var name = request.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludingId: id, cancellationToken);

        customer.Name = name;
        customer.Nickname = request.Nickname?.Trim();
        customer.Notes = request.Notes?.Trim();
        customer.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Idempotent, like packages and add-ons.</summary>
    public async Task<CustomerDetailDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var customer = await LoadAsync(id, cancellationToken);
        customer.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await LoadAsync(id, cancellationToken);

        if (await _db.Orders.AnyAsync(o => o.CustomerId == id || o.PayerId == id, cancellationToken))
        {
            throw new ConflictException(
                $"{customer.Name} has orders, so they can't be deleted. Deactivate them instead.");
        }

        customer.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomerDetailDto> MergeAsync(Guid id, Guid targetId, CancellationToken cancellationToken)
    {
        if (id == targetId)
        {
            throw new ValidationFailedException("A customer can't be merged into themselves.");
        }

        var source = await LoadAsync(id, cancellationToken);
        if (!await _db.Customers.AnyAsync(c => c.Id == targetId, cancellationToken))
        {
            throw NotFoundException.For("Customer", targetId);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Payments hang off orders, so moving an order moves its payments.
        var orders = await _db.Orders
            .Where(o => o.CustomerId == id || o.PayerId == id)
            .ToListAsync(cancellationToken);

        foreach (var order in orders)
        {
            if (order.CustomerId == id)
            {
                order.CustomerId = targetId;
            }

            if (order.PayerId == id)
            {
                order.PayerId = targetId;
            }

            // "Kee c/o Kee" after the merge is just Kee paying.
            if (order.PayerId == order.CustomerId)
            {
                order.PayerId = null;
            }
        }

        var entries = await _db.LoyaltyEntries.Where(e => e.CustomerId == id).ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            entry.CustomerId = targetId;
        }

        source.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(targetId, cancellationToken);
    }

    private sealed record Figures(
        Dictionary<Guid, decimal> Outstanding,
        Dictionary<Guid, int> Stamps,
        Dictionary<Guid, DateOnly> LastOrder,
        int PerReward);

    /// <summary>Tab balances, loyalty balances and last order dates for the given customers.</summary>
    private async Task<Figures> FiguresAsync(List<Guid> ids, CancellationToken cancellationToken)
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .Where(o => ids.Contains(o.CustomerId) || (o.PayerId != null && ids.Contains(o.PayerId.Value)))
            .Select(o => new { o.Id, o.CustomerId, o.PayerId, o.Total, o.OrderedOn })
            .ToListAsync(cancellationToken);

        var paid = await _db.PaidByOrderAsync(orders.Select(o => o.Id).ToList(), cancellationToken);

        var outstanding = orders
            .GroupBy(o => o.PayerId ?? o.CustomerId)
            .ToDictionary(g => g.Key, g => g.Sum(o => o.Total - paid.GetValueOrDefault(o.Id)));

        var lastOrder = orders
            .GroupBy(o => o.CustomerId)
            .ToDictionary(g => g.Key, g => g.Max(o => o.OrderedOn));

        var stamps = await _db.LoyaltyEntries
            .AsNoTracking()
            .Where(e => ids.Contains(e.CustomerId))
            .GroupBy(e => e.CustomerId)
            .Select(g => new { g.Key, Balance = g.Sum(e => e.Delta) })
            .ToDictionaryAsync(x => x.Key, x => x.Balance, cancellationToken);

        return new Figures(outstanding, stamps, lastOrder, await _settings.GetLoyaltyStampsPerRewardAsync(cancellationToken));
    }

    private static CustomerSummaryDto Summary(Customer c, Figures f)
    {
        var stamps = f.Stamps.GetValueOrDefault(c.Id);
        return new CustomerSummaryDto(
            c.Id, c.Name, c.Nickname, c.Notes, c.IsActive,
            f.Outstanding.GetValueOrDefault(c.Id),
            stamps,
            Math.Max(0, stamps) / f.PerReward,
            f.LastOrder.TryGetValue(c.Id, out var last) ? last : null);
    }

    private async Task EnsureNameIsFreeAsync(string name, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (await _db.Customers.AnyAsync(c => c.Name == name && c.Id != excludingId, cancellationToken))
        {
            throw new ConflictException($"A customer named \"{name}\" already exists.");
        }
    }

    private async Task<Customer> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
        ?? throw NotFoundException.For("Customer", id);
}
