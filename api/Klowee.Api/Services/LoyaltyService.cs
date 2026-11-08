using Klowee.Api.Common;
using Klowee.Api.Contracts.Customers;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

/// <summary>
/// Loyalty is a ledger (decision 016): the balance is the sum of the live
/// entries, and an entry tied to an order is voided (soft-deleted) when the
/// order stops qualifying for it, so the ledger only ever shows entries that
/// still stand.
/// </summary>
public class LoyaltyService : ILoyaltyService
{
    private readonly KloweeDbContext _db;
    private readonly ISiteSettingService _settings;

    public LoyaltyService(KloweeDbContext db, ISiteSettingService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<IReadOnlyList<LoyaltyEntryDto>> LedgerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        await EnsureCustomerExistsAsync(customerId, cancellationToken);

        var entries = await _db.LoyaltyEntries
            .AsNoTracking()
            .Where(e => e.CustomerId == customerId)
            .Select(e => new LoyaltyEntryDto(e.Id, e.Delta, e.Reason, e.OrderId, e.Notes, e.CreatedAt))
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

        if (request.Delta == 0)
        {
            throw new ValidationFailedException("An adjustment needs a non-zero delta.");
        }

        var entry = new LoyaltyEntry
        {
            CustomerId = customerId,
            Delta = request.Delta,
            Reason = LoyaltyReason.Adjustment,
            Notes = request.Notes?.Trim()
        };

        _db.LoyaltyEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        return new LoyaltyEntryDto(entry.Id, entry.Delta, entry.Reason, entry.OrderId, entry.Notes, entry.CreatedAt);
    }

    public async Task<int> BalanceAsync(Guid customerId, Guid? excludingOrderId, CancellationToken cancellationToken) =>
        await _db.LoyaltyEntries
            .Where(e => e.CustomerId == customerId && (excludingOrderId == null || e.OrderId != excludingOrderId))
            .SumAsync(e => e.Delta, cancellationToken);

    public async Task ReconcileAsync(Order order, decimal paid, CancellationToken cancellationToken)
    {
        var live = order.DeletedAt is null;
        var drinks = order.Items.Where(i => i.DeletedAt is null).Sum(i => i.Quantity);
        var isFree = order.DiscountType == DiscountType.LoyaltyFree;

        // One Earned entry per paid order, worth its drinks; the free drink earns nothing.
        var earned = live && SalesQueries.StatusFor(order.Total, paid) == PaymentStatus.Paid
            ? drinks - (isFree ? 1 : 0)
            : 0;

        var entries = await _db.LoyaltyEntries
            .Where(e => e.OrderId == order.Id)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var keptEarned = false;
        var keptRedeemed = false;

        foreach (var entry in entries)
        {
            var stillRight = entry.CustomerId == order.CustomerId && entry.Reason switch
            {
                LoyaltyReason.Earned => !keptEarned && earned > 0 && entry.Delta == earned,
                LoyaltyReason.Redeemed => !keptRedeemed && live && isFree,
                _ => true // adjustments are never tied to orders, but never touch one if they are
            };

            if (!stillRight)
            {
                entry.DeletedAt = now;
                continue;
            }

            keptEarned |= entry.Reason == LoyaltyReason.Earned;
            keptRedeemed |= entry.Reason == LoyaltyReason.Redeemed;
        }

        if (earned > 0 && !keptEarned)
        {
            _db.LoyaltyEntries.Add(new LoyaltyEntry
            {
                CustomerId = order.CustomerId,
                OrderId = order.Id,
                Delta = earned,
                Reason = LoyaltyReason.Earned
            });
        }

        if (live && isFree && !keptRedeemed)
        {
            // Availability is checked by the order service before the order is saved.
            _db.LoyaltyEntries.Add(new LoyaltyEntry
            {
                CustomerId = order.CustomerId,
                OrderId = order.Id,
                Delta = -await _settings.GetLoyaltyStampsPerRewardAsync(cancellationToken),
                Reason = LoyaltyReason.Redeemed
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCustomerExistsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            throw NotFoundException.For("Customer", customerId);
        }
    }
}
