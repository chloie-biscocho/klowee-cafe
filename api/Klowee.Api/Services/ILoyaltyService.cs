using Klowee.Api.Contracts.Customers;
using Klowee.Api.Entities;

namespace Klowee.Api.Services;

public interface ILoyaltyService
{
    /// <summary>The customer's ledger, newest first.</summary>
    Task<IReadOnlyList<LoyaltyEntryDto>> LedgerAsync(Guid customerId, CancellationToken cancellationToken);

    /// <summary>Writes an Adjustment entry (a correction by the owners).</summary>
    Task<LoyaltyEntryDto> AdjustAsync(Guid customerId, LoyaltyAdjustRequest request, CancellationToken cancellationToken);

    /// <summary>Sum of the customer's live entries, optionally leaving one order's entries out.</summary>
    Task<int> BalanceAsync(Guid customerId, Guid? excludingOrderId, CancellationToken cancellationToken);

    /// <summary>
    /// Makes the order's ledger entries match the order as it now stands
    /// (earned stamps when paid, a redemption while it carries LoyaltyFree,
    /// nothing once deleted). Saves.
    /// </summary>
    Task ReconcileAsync(Order order, decimal paid, CancellationToken cancellationToken);
}
