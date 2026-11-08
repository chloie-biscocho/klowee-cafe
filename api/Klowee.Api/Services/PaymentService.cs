using Klowee.Api.Common;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

/// <summary>
/// Payments always belong to one order, so status and stamps stay per order.
/// A transfer that covers several orders is split by <see cref="SettleAsync"/>.
/// Every change ends with a loyalty reconcile: an order that becomes Paid earns
/// its stamps, one that drops below Paid loses them.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly KloweeDbContext _db;
    private readonly ILoyaltyService _loyalty;
    private readonly IOrderService _orders;

    public PaymentService(KloweeDbContext db, ILoyaltyService loyalty, IOrderService orders)
    {
        _db = db;
        _loyalty = loyalty;
        _orders = orders;
    }

    public async Task<OrderDetailDto> AddAsync(Guid orderId, PaymentRequest request, CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, cancellationToken);
        var paid = (await _db.PaidByOrderAsync([orderId], cancellationToken)).GetValueOrDefault(orderId);
        var owed = order.Total - paid;

        if (request.Amount > owed)
        {
            throw new ConflictException(owed == 0m
                ? "This order is already paid in full."
                : $"₱{request.Amount:0.00} is more than this order owes (₱{owed:0.00}).");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        _db.Payments.Add(NewPayment(orderId, request.Amount, request));
        await _db.SaveChangesAsync(cancellationToken);
        await _loyalty.ReconcileAsync(order, paid + request.Amount, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await _orders.GetAsync(orderId, cancellationToken);
    }

    public async Task DeleteAsync(Guid orderId, Guid paymentId, CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, cancellationToken);
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId && p.OrderId == orderId, cancellationToken)
            ?? throw NotFoundException.For("Payment", paymentId);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        payment.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var paid = (await _db.PaidByOrderAsync([orderId], cancellationToken)).GetValueOrDefault(orderId);
        await _loyalty.ReconcileAsync(order, paid, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentDto>> SettleAsync(
        Guid customerId,
        PaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            throw NotFoundException.For("Customer", customerId);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        // The tab: orders this customer pays for (their own, unless someone
        // else pays, plus any they cover for others), oldest first.
        var tab = (await _db.Orders
                .Include(o => o.Items)
                .Where(o => (o.PayerId ?? o.CustomerId) == customerId)
                .ToListAsync(cancellationToken))
            .OrderBy(o => o.OrderedOn).ThenBy(o => o.CreatedAt).ThenBy(o => o.Id)
            .ToList();

        var paid = await _db.PaidByOrderAsync(tab.Select(o => o.Id).ToList(), cancellationToken);
        var open = tab
            .Select(o => (Order: o, Owed: o.Total - paid.GetValueOrDefault(o.Id)))
            .Where(x => x.Owed > 0m)
            .ToList();

        var outstanding = open.Sum(x => x.Owed);
        if (outstanding == 0m)
        {
            throw new ConflictException("Nothing is outstanding on this tab.");
        }

        if (request.Amount > outstanding)
        {
            throw new ConflictException($"₱{request.Amount:0.00} is more than the tab owes (₱{outstanding:0.00}).");
        }

        // Oldest first: each order takes what it owes until the money runs out;
        // the last one touched may be left partly paid.
        var remaining = request.Amount;
        var created = new List<(Payment Payment, Order Order, decimal PaidAfter)>();
        foreach (var (order, owed) in open)
        {
            if (remaining == 0m)
            {
                break;
            }

            var amount = Math.Min(owed, remaining);
            remaining -= amount;

            var payment = NewPayment(order.Id, amount, request);
            _db.Payments.Add(payment);
            created.Add((payment, order, paid.GetValueOrDefault(order.Id) + amount));
        }

        await _db.SaveChangesAsync(cancellationToken);
        foreach (var (_, order, paidAfter) in created)
        {
            await _loyalty.ReconcileAsync(order, paidAfter, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return created
            .Select(c => new PaymentDto(c.Payment.Id, c.Payment.OrderId, c.Payment.Amount, c.Payment.Channel,
                c.Payment.PaidOn, c.Payment.Reference, c.Payment.Notes))
            .ToList();
    }

    private static Payment NewPayment(Guid orderId, decimal amount, PaymentRequest request) => new()
    {
        OrderId = orderId,
        Amount = amount,
        Channel = request.Channel,
        PaidOn = request.PaidOn,
        Reference = request.Reference?.Trim(),
        Notes = request.Notes?.Trim()
    };

    private async Task<Order> LoadOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
        ?? throw NotFoundException.For("Order", orderId);
}
