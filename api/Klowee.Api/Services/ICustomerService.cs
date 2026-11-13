using Klowee.Api.Contracts.Customers;

namespace Klowee.Api.Services;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerSummaryDto>> ListAsync(bool includeInactive, string? search, CancellationToken cancellationToken);

    /// <summary>The customer with their 20 most recent orders (as drinker or payer).</summary>
    Task<CustomerDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<CustomerDetailDto> CreateAsync(CustomerRequest request, CancellationToken cancellationToken);

    Task<CustomerDetailDto> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken);

    Task<CustomerDetailDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);

    /// <summary>409 while the customer has orders (as drinker or payer): deactivate instead.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Moves orders, payer references, stamps and used rewards (club by club) to the target, then deletes the source.</summary>
    Task<CustomerDetailDto> MergeAsync(Guid id, Guid targetId, CancellationToken cancellationToken);
}
