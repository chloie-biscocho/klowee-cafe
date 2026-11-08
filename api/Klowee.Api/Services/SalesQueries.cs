using Klowee.Api.Contracts.Orders;
using Klowee.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

/// <summary>
/// Reads shared by the sales services. Money is summed in memory, not in SQL:
/// the test suite's SQLite cannot SUM or compare decimals, and office sales are
/// tens of rows a month, so loading them costs nothing.
/// </summary>
public static class SalesQueries
{
    /// <summary>Unpaid (nothing paid), Partial, or Paid (paid ≥ total; a zero-total order is Paid).</summary>
    public static PaymentStatus StatusFor(decimal total, decimal paid) =>
        paid >= total ? PaymentStatus.Paid
        : paid == 0m ? PaymentStatus.Unpaid
        : PaymentStatus.Partial;

    /// <summary>Total paid per order, for the given orders (orders with no payments are absent).</summary>
    public static async Task<Dictionary<Guid, decimal>> PaidByOrderAsync(
        this KloweeDbContext db,
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        var payments = await db.Payments
            .AsNoTracking()
            .Where(p => orderIds.Contains(p.OrderId))
            .Select(p => new { p.OrderId, p.Amount })
            .ToListAsync(cancellationToken);

        return payments
            .GroupBy(p => p.OrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
    }

    /// <summary>Every live payment's amount, per order.</summary>
    public static async Task<Dictionary<Guid, decimal>> PaidByOrderAsync(
        this KloweeDbContext db,
        CancellationToken cancellationToken)
    {
        var payments = await db.Payments
            .AsNoTracking()
            .Select(p => new { p.OrderId, p.Amount })
            .ToListAsync(cancellationToken);

        return payments
            .GroupBy(p => p.OrderId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
    }
}
