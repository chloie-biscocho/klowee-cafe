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
    private readonly ILoyaltyService _loyalty;

    public CustomerService(KloweeDbContext db, IOrderService orders, ILoyaltyService loyalty)
    {
        _db = db;
        _orders = orders;
        _loyalty = loyalty;
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
            summary.OutstandingBalance, summary.Clubs, summary.LastOrderOn,
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

        // Stamps move club by club: each entry keeps its club, so the target's
        // card on every club is the two cards' stamps added up.
        var entries = await _db.LoyaltyEntries.Where(e => e.CustomerId == id).ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            entry.CustomerId = targetId;
        }

        await MoveRedemptionsAsync(id, targetId, cancellationToken);
        await MoveMembershipsAsync(id, targetId, cancellationToken);

        source.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(targetId, cancellationToken);
    }

    /// <summary>
    /// Moves the source's used rewards to the target. Both may have used the
    /// same reward on their first card, and a reward is used once per card, so
    /// each reward's uses are renumbered onto the target's cards in order
    /// (first use, card 1; second, card 2): every use survives the merge.
    /// </summary>
    private async Task MoveRedemptionsAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken)
    {
        var redemptions = await _db.LoyaltyRedemptions
            .Where(r => r.CustomerId == sourceId || r.CustomerId == targetId)
            .ToListAsync(cancellationToken);
        if (!redemptions.Any(r => r.CustomerId == sourceId))
        {
            return;
        }

        var renumbered = redemptions
            .GroupBy(r => (r.ClubId, r.RewardId))
            .SelectMany(g => g
                .OrderBy(r => r.Cycle).ThenBy(r => r.CustomerId == targetId ? 0 : 1).ThenBy(r => r.Id)
                .Select((r, i) => (Redemption: r, Cycle: i)))
            .ToList();

        // Two steps, so the unique (customer, club, cycle, reward) index never
        // sees two rows on one card mid-update: first out of the way, then into place.
        foreach (var (redemption, cycle) in renumbered)
        {
            redemption.CustomerId = targetId;
            redemption.Cycle = -100_001 - cycle;
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var (redemption, cycle) in renumbered)
        {
            redemption.Cycle = cycle;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Moves the source's cards to the target. When both hold a card in the same
    /// club, the one held longer wins (an Active card over a requested one, then
    /// the earlier joined_on, null meaning "since the beginning").
    /// </summary>
    private async Task MoveMembershipsAsync(Guid sourceId, Guid targetId, CancellationToken cancellationToken)
    {
        var memberships = await _db.LoyaltyMemberships
            .Where(m => m.CustomerId == sourceId || m.CustomerId == targetId)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var keepers = new List<LoyaltyMembership>();

        foreach (var club in memberships.GroupBy(m => m.ClubId))
        {
            var keep = club
                .OrderBy(m => m.Status == MembershipStatus.Active ? 0 : 1)
                .ThenBy(m => m.JoinedOn ?? DateOnly.MinValue)
                .First();
            foreach (var other in club.Where(m => m != keep))
            {
                other.DeletedAt = now;
            }

            keepers.Add(keep);
        }

        // Two saves: the losers leave before the winners move in, so the unique
        // (customer, club) index never sees two live cards at once.
        await _db.SaveChangesAsync(cancellationToken);
        foreach (var keep in keepers)
        {
            keep.CustomerId = targetId;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private sealed record Figures(
        Dictionary<Guid, decimal> Outstanding,
        Dictionary<Guid, IReadOnlyList<CustomerClubDto>> Clubs,
        Dictionary<Guid, DateOnly> LastOrder);

    /// <summary>Tab balances, loyalty cards and last order dates for the given customers.</summary>
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

        var clubs = await _loyalty.CardsAsync(ids, cancellationToken);

        return new Figures(outstanding, clubs, lastOrder);
    }

    private static CustomerSummaryDto Summary(Customer c, Figures f) =>
        new(
            c.Id, c.Name, c.Nickname, c.Notes, c.IsActive,
            f.Outstanding.GetValueOrDefault(c.Id),
            f.Clubs.GetValueOrDefault(c.Id) ?? [],
            f.LastOrder.TryGetValue(c.Id, out var last) ? last : null);

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
