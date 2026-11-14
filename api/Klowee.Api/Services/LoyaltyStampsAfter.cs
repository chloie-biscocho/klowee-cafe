using Klowee.Api.Contracts.Loyalty;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

/// <summary>
/// Handover 17a, decision 020: on paper a card was stamped when the drink was
/// ordered; in the system, when the order is paid. A carried-over card's
/// position came from the paper card, so it already counts every drink ordered
/// up to the carry-over day. <see cref="LoyaltyMembership.StampsAfter"/> holds
/// that day; <see cref="LoyaltyService.Stamps"/> refuses orders on or before it.
/// </summary>
public static class LoyaltyStampsAfter
{
    public const string MarkerName = "loyalty-stamps-after";
    private static readonly TimeZoneInfo Manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

    public static DateOnly ManilaDate(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, Manila).DateTime);

    /// <summary>
    /// One-time startup step, behind its own marker: gives every card the
    /// carry-over already created ("since the beginning", JoinedOn null) the
    /// carry-over day as StampsAfter. It only sets dates; it voids nothing
    /// (production's stray stamps are listed and voided by the owner, see
    /// <see cref="StrayStampService"/>). Returns the cards dated, or null when
    /// there was nothing to do (already ran, or no carry-over yet).
    /// </summary>
    public static async Task<int?> BackfillAsync(KloweeDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.DataMigrations.AnyAsync(m => m.Name == MarkerName, cancellationToken))
        {
            return null;
        }

        var carryOver = await db.DataMigrations.FirstOrDefaultAsync(m => m.Name == LoyaltyCarryOver.MarkerName, cancellationToken);
        if (carryOver is null)
        {
            // No carry-over yet: when it runs it dates its own cards. Try again next start.
            return null;
        }

        var day = ManilaDate(carryOver.CreatedAt);
        var cards = await db.LoyaltyMemberships
            .Where(m => m.Status == MembershipStatus.Active && m.JoinedOn == null && m.StampsAfter == null)
            .ToListAsync(cancellationToken);
        foreach (var card in cards)
        {
            card.StampsAfter = day;
        }

        db.DataMigrations.Add(new DataMigration
        {
            Name = MarkerName,
            Summary = $"Carried-over cards dated: {cards.Count}; orders on or before {day:yyyy-MM-dd} never stamp them."
        });
        await db.SaveChangesAsync(cancellationToken);
        return cards.Count;
    }
}

public interface IStrayStampService
{
    Task<StrayStampsDto> ListAsync(CancellationToken cancellationToken);

    /// <summary>Voids the listed stamps through the normal reconcile; money never moves.</summary>
    Task<StrayStampsDto> VoidAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Stamps given by mistake before handover 17a: an order dated on or before a
/// carried-over card's StampsAfter, paid later, stamped the card again. Lists
/// them (read only), and on request re-runs ReconcileAsync on each order with
/// its lines treated as changed, so the stamp is re-derived under the rule and
/// voided. Orders that used a reward the stray stamps unlocked are listed, never undone.
/// </summary>
public class StrayStampService : IStrayStampService
{
    private readonly KloweeDbContext _db;
    private readonly ILoyaltyService _loyalty;

    public StrayStampService(KloweeDbContext db, ILoyaltyService loyalty)
    {
        _db = db;
        _loyalty = loyalty;
    }

    public async Task<StrayStampsDto> ListAsync(CancellationToken cancellationToken)
    {
        var cards = await _db.LoyaltyMemberships
            .AsNoTracking()
            .Where(m => m.Status == MembershipStatus.Active && m.StampsAfter != null)
            .Select(m => new { m.CustomerId, m.ClubId, StampsAfter = m.StampsAfter!.Value, Customer = m.Customer!.Name, Club = m.Club!.Name, m.Club.SlotCount })
            .ToListAsync(cancellationToken);
        var customerIds = cards.Select(c => c.CustomerId).Distinct().ToList();

        var entries = await _db.LoyaltyEntries
            .AsNoTracking()
            .Where(e => customerIds.Contains(e.CustomerId))
            .Select(e => new { e.Id, e.CustomerId, e.ClubId, e.Delta, e.Reason, e.OrderId, OrderedOn = e.Order == null ? (DateOnly?)null : e.Order.OrderedOn })
            .ToListAsync(cancellationToken);
        var redemptions = await _db.LoyaltyRedemptions
            .AsNoTracking()
            .Where(r => customerIds.Contains(r.CustomerId) && r.OrderId != null)
            .Select(r => new { r.CustomerId, r.ClubId, r.Cycle, r.RewardId, r.OrderId, r.Reward!.Slot, r.Reward.Label, OrderedOn = r.Order!.OrderedOn })
            .ToListAsync(cancellationToken);

        var stamps = new List<StrayStampDto>();
        var positions = new List<StrayPositionDto>();
        var rewards = new List<StrayRewardDto>();
        foreach (var card in cards.OrderBy(c => c.Customer).ThenBy(c => c.Club))
        {
            var mine = entries.Where(e => e.CustomerId == card.CustomerId && e.ClubId == card.ClubId).ToList();
            var stray = mine
                .Where(e => e.Reason == LoyaltyReason.Earned && e.OrderedOn is { } on && on <= card.StampsAfter)
                .OrderBy(e => e.OrderedOn)
                .ToList();
            if (stray.Count == 0)
            {
                continue;
            }

            stamps.AddRange(stray.Select(e => new StrayStampDto(card.CustomerId, card.Customer, card.ClubId, card.Club, e.OrderId!.Value, e.OrderedOn!.Value, e.Delta)));
            var before = Math.Max(0, mine.Sum(e => e.Delta));
            var after = Math.Max(0, before - stray.Sum(e => e.Delta));
            positions.Add(new StrayPositionDto(card.Customer, card.Club, card.StampsAfter, Describe(before, card.SlotCount), Describe(after, card.SlotCount)));

            // A reward an order already used that the card would no longer have earned.
            rewards.AddRange(redemptions
                .Where(r => r.CustomerId == card.CustomerId && r.ClubId == card.ClubId
                    && !LoyaltyCard.IsEarned(after, card.SlotCount, r.Slot, r.Cycle))
                .Select(r => new StrayRewardDto(card.Customer, card.Club, r.Label, r.Cycle + 1, r.Slot, r.OrderId!.Value, r.OrderedOn)));
        }

        return new StrayStampsDto(stamps, positions, rewards);
    }

    public async Task<StrayStampsDto> VoidAsync(CancellationToken cancellationToken)
    {
        var listed = await ListAsync(cancellationToken);
        if (listed.Stamps.Count == 0)
        {
            return listed;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var orderId in listed.Stamps.Select(s => s.OrderId).Distinct())
        {
            var order = await _db.Orders
                .Include(o => o.Items).ThenInclude(i => i.AddOns)
                .FirstAsync(o => o.Id == orderId, cancellationToken);
            var paid = (await _db.PaidByOrderAsync([order.Id], cancellationToken)).GetValueOrDefault(order.Id);
            // "Lines changed" makes reconcile re-derive the stamps instead of keeping a paid order's.
            await _loyalty.ReconcileAsync(order, paid, linesChanged: true, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await ListAsync(cancellationToken);
    }

    /// <summary>"card 1, 5 of 12"</summary>
    private static string Describe(int stamps, int slotCount) =>
        $"card {stamps / slotCount + 1}, {stamps % slotCount} of {slotCount}";
}
