using Klowee.Api.Common;
using Klowee.Api.Contracts.Customers;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

/// <summary>
/// Loyalty clubs (decision 020) on the ledger of decision 016. Stamps are
/// ledger entries per club; a customer's place on a card and which rewards
/// are earned are derived from them (<see cref="LoyaltyCard"/>); a used
/// reward is a redemption row. Stamps and rewards belong to the drinker.
/// </summary>
public class LoyaltyService : ILoyaltyService
{
    private readonly KloweeDbContext _db;

    public LoyaltyService(KloweeDbContext db) => _db = db;

    /// <summary>Whether an order on this date stamps this club: active, and on or after its launch.</summary>
    public static bool Earns(LoyaltyClub club, DateOnly orderedOn) =>
        club.IsActive && (club.ActivatedOn is null || orderedOn >= club.ActivatedOn);

    public async Task<IReadOnlyList<LoyaltyEntryDto>> LedgerAsync(Guid customerId, Guid? clubId, CancellationToken cancellationToken)
    {
        await EnsureCustomerExistsAsync(customerId, cancellationToken);

        var entries = await _db.LoyaltyEntries
            .AsNoTracking()
            .Where(e => e.CustomerId == customerId && (clubId == null || e.ClubId == clubId))
            .Select(e => new LoyaltyEntryDto(e.Id, e.ClubId, e.Club!.Name, e.Delta, e.Reason, e.OrderId, e.Notes, e.CreatedAt))
            .ToListAsync(cancellationToken);

        // Newest first, sorted in memory (SQLite in the tests can't order by created_at).
        return entries.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id).ToList();
    }

    public async Task<LoyaltyEntryDto> AdjustAsync(
        Guid customerId,
        LoyaltyAdjustRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureCustomerExistsAsync(customerId, cancellationToken);
        var club = await ActiveClubAsync(request.ClubId, cancellationToken);

        if (request.Delta == 0)
        {
            throw new ValidationFailedException("An adjustment needs a non-zero delta.");
        }

        var entry = new LoyaltyEntry
        {
            CustomerId = customerId,
            ClubId = club.Id,
            Delta = request.Delta,
            Reason = LoyaltyReason.Adjustment,
            Notes = request.Notes?.Trim()
        };

        _db.LoyaltyEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        return new LoyaltyEntryDto(entry.Id, club.Id, club.Name, entry.Delta, entry.Reason, entry.OrderId, entry.Notes, entry.CreatedAt);
    }

    public async Task<LoyaltyRedemptionDto> RedeemAsync(
        Guid customerId,
        LoyaltyRedemptionRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureCustomerExistsAsync(customerId, cancellationToken);
        var club = await ActiveClubAsync(request.ClubId, cancellationToken);
        var reward = club.Rewards.FirstOrDefault(r => r.Id == request.RewardId)
            ?? throw new ValidationFailedException($"Reward '{request.RewardId}' is not on {club.Name}.");

        var card = await CardAsync(customerId, club, [], cancellationToken);
        if (!card.Available.Any(v => v.RewardId == reward.Id && v.Cycle == request.Cycle))
        {
            throw new ConflictException(
                $"{reward.Label} on card {request.Cycle + 1} of {club.Name} isn't available: it is not earned yet, or already used.");
        }

        var redemption = new LoyaltyRedemption
        {
            CustomerId = customerId,
            ClubId = club.Id,
            RewardId = reward.Id,
            Cycle = request.Cycle,
            Note = request.Note?.Trim()
        };

        _db.LoyaltyRedemptions.Add(redemption);
        await _db.SaveChangesAsync(cancellationToken);

        return new LoyaltyRedemptionDto(
            redemption.Id, club.Id, reward.Id, redemption.Cycle, null, redemption.Note, redemption.CreatedAt);
    }

    public async Task UnredeemAsync(Guid customerId, Guid redemptionId, CancellationToken cancellationToken)
    {
        var redemption = await _db.LoyaltyRedemptions
            .FirstOrDefaultAsync(r => r.Id == redemptionId && r.CustomerId == customerId, cancellationToken)
            ?? throw NotFoundException.For("Redemption", redemptionId);

        if (redemption.OrderId is not null)
        {
            throw new ConflictException(
                "An order used this reward. Remove the reward from that order (or delete the order) to make it available again.");
        }

        redemption.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, IReadOnlyList<CustomerClubDto>>> CardsAsync(
        IReadOnlyCollection<Guid> customerIds,
        CancellationToken cancellationToken)
    {
        var clubs = await _db.LoyaltyClubs
            .AsNoTracking()
            .Include(c => c.Rewards)
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken);
        clubs = clubs.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();

        var result = customerIds.Distinct().ToDictionary(id => id, _ => (IReadOnlyList<CustomerClubDto>)[]);
        if (clubs.Count == 0 || result.Count == 0)
        {
            return result;
        }

        var ids = result.Keys.ToList();
        var clubIds = clubs.Select(c => c.Id).ToList();

        var sums = (await _db.LoyaltyEntries
                .AsNoTracking()
                .Where(e => ids.Contains(e.CustomerId) && clubIds.Contains(e.ClubId))
                .Select(e => new { e.CustomerId, e.ClubId, e.Delta })
                .ToListAsync(cancellationToken))
            .GroupBy(e => (e.CustomerId, e.ClubId))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Delta));

        var redemptions = (await _db.LoyaltyRedemptions
                .AsNoTracking()
                .Where(r => ids.Contains(r.CustomerId) && clubIds.Contains(r.ClubId))
                .Select(r => new { r.CustomerId, r.ClubId, Redemption = new CardRedemption(r.Id, r.RewardId, r.Cycle, r.OrderId) })
                .ToListAsync(cancellationToken))
            .ToLookup(r => (r.CustomerId, r.ClubId), r => r.Redemption);

        var pending = await PendingDrinksAsync(ids, clubs, cancellationToken);

        foreach (var customerId in ids)
        {
            result[customerId] = clubs.Select(club =>
            {
                var card = LoyaltyCard.For(
                    sums.GetValueOrDefault((customerId, club.Id)),
                    club.SlotCount,
                    Rewards(club),
                    redemptions[(customerId, club.Id)].ToList());

                return new CustomerClubDto(
                    club.Id, club.Name, club.Style, club.SlotCount, card.Position, card.Cycle,
                    card.Rewards.Select(v => new CustomerRewardDto(
                        v.RewardId, v.Slot, v.Label, v.Type, v.State.ToString().ToLowerInvariant(),
                        v.Cycle, v.RedemptionId, v.OrderId)).ToList(),
                    pending.GetValueOrDefault((customerId, club.Id)));
            }).ToList();
        }

        return result;
    }

    public async Task<CardState> CardAsync(
        Guid customerId,
        LoyaltyClub club,
        IReadOnlyCollection<Guid> ignoringRedemptions,
        CancellationToken cancellationToken)
    {
        var sum = (await _db.LoyaltyEntries
            .Where(e => e.CustomerId == customerId && e.ClubId == club.Id)
            .Select(e => e.Delta)
            .ToListAsync(cancellationToken)).Sum();

        var redemptions = await _db.LoyaltyRedemptions
            .Where(r => r.CustomerId == customerId && r.ClubId == club.Id && !ignoringRedemptions.Contains(r.Id))
            .Select(r => new CardRedemption(r.Id, r.RewardId, r.Cycle, r.OrderId))
            .ToListAsync(cancellationToken);

        return LoyaltyCard.For(sum, club.SlotCount, Rewards(club), redemptions);
    }

    public async Task ReconcileAsync(Order order, decimal paid, bool linesChanged, CancellationToken cancellationToken)
    {
        var isPaid = order.DeletedAt is null && SalesQueries.StatusFor(order.Total, paid) == PaymentStatus.Paid;
        var entries = await _db.LoyaltyEntries
            .Where(e => e.OrderId == order.Id && e.Reason != LoyaltyReason.Adjustment)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        // The old single card's redemptions are never written any more.
        foreach (var redeemed in entries.Where(e => e.Reason == LoyaltyReason.Redeemed))
        {
            redeemed.DeletedAt = now;
        }

        var earned = entries.Where(e => e.Reason == LoyaltyReason.Earned).ToList();

        // Rule 2's "resolved when the order becomes Paid": a paid order that
        // already has its stamps keeps them, even if a category moved since.
        var keep = isPaid && !linesChanged && earned.Count > 0 && earned.All(e => e.CustomerId == order.CustomerId);
        if (!keep)
        {
            // An order the carry-over already counted in someone's starting balance never stamps again.
            var carriedOver = await _db.LoyaltyEntries
                .IgnoreQueryFilters()
                .AnyAsync(e => e.OrderId == order.Id && e.CarriedOverAt != null, cancellationToken);
            var wanted = isPaid && !carriedOver ? await DrinksByClubAsync(order, cancellationToken) : [];

            foreach (var entry in earned)
            {
                if (entry.CustomerId == order.CustomerId
                    && wanted.TryGetValue(entry.ClubId, out var drinks)
                    && drinks == entry.Delta)
                {
                    wanted.Remove(entry.ClubId);
                    continue;
                }

                entry.DeletedAt = now;
            }

            foreach (var (clubId, drinks) in wanted)
            {
                _db.LoyaltyEntries.Add(new LoyaltyEntry
                {
                    CustomerId = order.CustomerId,
                    ClubId = clubId,
                    OrderId = order.Id,
                    Delta = drinks,
                    Reason = LoyaltyReason.Earned
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Drink units per club for an order, counting only clubs it stamps
    /// (active, and launched on or before the order date). Drinks with a reward
    /// still count. An old LoyaltyFree order's free drink never did, and still doesn't.
    /// </summary>
    private async Task<Dictionary<Guid, int>> DrinksByClubAsync(Order order, CancellationToken cancellationToken)
    {
        var lines = order.Items.Where(i => i.DeletedAt is null).OrderBy(i => i.SortOrder).ToList();
        var map = await LoyaltyClubMap.LoadAsync(_db, lines.Select(l => l.MenuVersionItemId).Distinct().ToList(), cancellationToken);
        var clubs = await _db.LoyaltyClubs.AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);

        var drinks = new Dictionary<Guid, int>();
        foreach (var line in lines)
        {
            if (map.ForVersionItem(line.MenuVersionItemId) is { } clubId
                && clubs.TryGetValue(clubId, out var club)
                && Earns(club, order.OrderedOn))
            {
                drinks[clubId] = drinks.GetValueOrDefault(clubId) + line.Quantity;
            }
        }

        if (order.DiscountType == DiscountType.LoyaltyFree && lines.Count > 0)
        {
            var free = lines.MinBy(l => l.UnitPrice + l.AddOns.Where(a => a.DeletedAt is null).Sum(a => a.UnitPrice))!;
            if (map.ForVersionItem(free.MenuVersionItemId) is { } freeClub && drinks.ContainsKey(freeClub))
            {
                drinks[freeClub]--;
            }
        }

        return drinks.Where(d => d.Value > 0).ToDictionary(d => d.Key, d => d.Value);
    }

    /// <summary>Drinks per (customer, club) on each customer's orders that aren't Paid yet.</summary>
    private async Task<Dictionary<(Guid Customer, Guid Club), int>> PendingDrinksAsync(
        List<Guid> customerIds,
        List<LoyaltyClub> clubs,
        CancellationToken cancellationToken)
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .Where(o => customerIds.Contains(o.CustomerId))
            .Select(o => new { o.Id, o.CustomerId, o.OrderedOn, o.Total })
            .ToListAsync(cancellationToken);
        var paid = await _db.PaidByOrderAsync(orders.Select(o => o.Id).ToList(), cancellationToken);
        var open = orders
            .Where(o => SalesQueries.StatusFor(o.Total, paid.GetValueOrDefault(o.Id)) != PaymentStatus.Paid)
            .ToDictionary(o => o.Id);

        var result = new Dictionary<(Guid, Guid), int>();
        if (open.Count == 0)
        {
            return result;
        }

        var openIds = open.Keys.ToList();
        var lines = await _db.OrderItems
            .AsNoTracking()
            .Where(i => openIds.Contains(i.OrderId))
            .Select(i => new { i.OrderId, i.MenuVersionItemId, i.Quantity })
            .ToListAsync(cancellationToken);
        var map = await LoyaltyClubMap.LoadAsync(_db, lines.Select(l => l.MenuVersionItemId).Distinct().ToList(), cancellationToken);
        var byId = clubs.ToDictionary(c => c.Id);

        foreach (var line in lines)
        {
            var order = open[line.OrderId];
            if (map.ForVersionItem(line.MenuVersionItemId) is { } clubId
                && byId.TryGetValue(clubId, out var club)
                && Earns(club, order.OrderedOn))
            {
                result[(order.CustomerId, clubId)] = result.GetValueOrDefault((order.CustomerId, clubId)) + line.Quantity;
            }
        }

        return result;
    }

    private static List<CardReward> Rewards(LoyaltyClub club) =>
        club.Rewards.Where(r => r.DeletedAt is null)
            .Select(r => new CardReward(r.Id, r.Slot, r.Label, r.Type))
            .ToList();

    private async Task<LoyaltyClub> ActiveClubAsync(Guid clubId, CancellationToken cancellationToken)
    {
        var club = await _db.LoyaltyClubs
            .AsNoTracking()
            .Include(c => c.Rewards)
            .FirstOrDefaultAsync(c => c.Id == clubId, cancellationToken)
            ?? throw new ValidationFailedException($"Loyalty club '{clubId}' does not exist.");

        if (!club.IsActive)
        {
            throw new ConflictException($"{club.Name} isn't active. Activate it under Menu → Loyalty cards first.");
        }

        return club;
    }

    private async Task EnsureCustomerExistsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            throw NotFoundException.For("Customer", customerId);
        }
    }
}
