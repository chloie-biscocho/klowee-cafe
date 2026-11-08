using Klowee.Api.Contracts.Orders;

namespace Klowee.Api.Services;

public interface IPaymentService
{
    /// <summary>Records a payment against one order. 409 if it would overpay. Returns the order.</summary>
    Task<OrderDetailDto> AddAsync(Guid orderId, PaymentRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid orderId, Guid paymentId, CancellationToken cancellationToken);

    /// <summary>
    /// Spreads one amount over the customer's tab, oldest order first, one
    /// payment per order touched. 409 if it is more than the tab owes.
    /// </summary>
    Task<IReadOnlyList<PaymentDto>> SettleAsync(Guid customerId, PaymentRequest request, CancellationToken cancellationToken);
}
