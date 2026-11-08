using Klowee.Api.Contracts.Orders;

namespace Klowee.Api.Services;

public record OrderListQuery(
    DateOnly? From,
    DateOnly? To,
    Guid? CustomerId,
    PaymentStatus? Status,
    int Page,
    int PageSize);

public interface IOrderService
{
    Task<PagedResult<OrderSummaryDto>> ListAsync(OrderListQuery query, CancellationToken cancellationToken);

    Task<OrderDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<OrderDetailDto> CreateAsync(OrderRequest request, CancellationToken cancellationToken);

    /// <summary>Replaces the order. 409 once it has payments.</summary>
    Task<OrderDetailDto> UpdateAsync(Guid id, OrderRequest request, CancellationToken cancellationToken);

    /// <summary>Soft-deletes the order, its items, add-ons and payments, and voids its loyalty entries.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Summaries of the given orders, newest first (for customer detail).</summary>
    Task<IReadOnlyList<OrderSummaryDto>> SummariesAsync(IQueryable<Entities.Order> orders, CancellationToken cancellationToken);
}
