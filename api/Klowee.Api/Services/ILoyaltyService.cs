using Klowee.Api.Contracts.Customers;
using Klowee.Api.Entities;

namespace Klowee.Api.Services;

public interface ILoyaltyService
{
    /// <summary>The customer's ledger, newest first; one club's entries when <paramref name="clubId"/> is set.</summary>
    Task<IReadOnlyList<LoyaltyEntryDto>> LedgerAsync(Guid customerId, Guid? clubId, CancellationToken cancellationToken);

    /// <summary>Writes an Adjustment entry on one club (a correction by the owners).</summary>
    Task<LoyaltyEntryDto> AdjustAsync(Guid customerId, LoyaltyAdjustRequest request, CancellationToken cancellationToken);

    /// <summary>Marks an available reward used by hand, with no order.</summary>
    Task<LoyaltyRedemptionDto> RedeemAsync(Guid customerId, LoyaltyRedemptionRequest request, CancellationToken cancellationToken);

    /// <summary>Makes a hand-marked reward available again. Refused (409) when an order used it.</summary>
    Task UnredeemAsync(Guid customerId, Guid redemptionId, CancellationToken cancellationToken);

    /// <summary>Gives a card (Active from today) and the cards that come with it; activates a requested one.</summary>
    Task<LoyaltyMembershipDto> GiveCardAsync(Guid customerId, LoyaltyMembershipRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a card away. Refused (409) while it has live stamps or used
    /// rewards, unless <paramref name="force"/>, which voids them.
    /// </summary>
    Task TakeCardAsync(Guid customerId, Guid membershipId, bool force, CancellationToken cancellationToken);

    /// <summary>Each customer's card on every active club, in club order.</summary>
    Task<Dictionary<Guid, IReadOnlyList<CustomerClubDto>>> CardsAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken);

    /// <summary>
    /// One customer's card on one club, as if the given redemptions did not
    /// exist (an order being re-saved gives back the rewards it is about to release).
    /// </summary>
    Task<CardState> CardAsync(Guid customerId, LoyaltyClub club, IReadOnlyCollection<Guid> ignoringRedemptions, CancellationToken cancellationToken);

    /// <summary>
    /// Makes the order's ledger entries match the order as it now stands: when
    /// it is Paid, one Earned entry per club its drinks stamp; otherwise none.
    /// The clubs are resolved when the order becomes Paid and kept while it
    /// stays Paid, unless <paramref name="linesChanged"/>. Saves.
    /// </summary>
    Task ReconcileAsync(Order order, decimal paid, bool linesChanged, CancellationToken cancellationToken);
}
