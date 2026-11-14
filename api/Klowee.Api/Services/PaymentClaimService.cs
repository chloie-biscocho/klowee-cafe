using Klowee.Api.Common;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Contracts.Tabs;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

public interface IPaymentClaimService
{
    Task<IReadOnlyList<PaymentClaimDto>> ListAsync(PaymentClaimStatus? status, CancellationToken cancellationToken);
    Task<PaymentClaimDto> ConfirmAsync(Guid id, ConfirmClaimRequest request, CancellationToken cancellationToken);
    Task<PaymentClaimDto> RejectAsync(Guid id, RejectClaimRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Customers' "I've paid" reports (decision 021). A claim moves no money:
/// confirming one runs the usual settle (oldest order first, stamps through
/// ReconcileAsync) with the claim's amount, reference and channel, in one
/// transaction, and links the payments to it. Rejecting records why.
/// </summary>
public class PaymentClaimService : IPaymentClaimService
{
    private readonly KloweeDbContext _db;
    private readonly IPaymentService _payments;
    private readonly IClock _clock;
    private readonly ICurrentUser _user;

    public PaymentClaimService(KloweeDbContext db, IPaymentService payments, IClock clock, ICurrentUser user)
    {
        _db = db;
        _payments = payments;
        _clock = clock;
        _user = user;
    }

    public async Task<IReadOnlyList<PaymentClaimDto>> ListAsync(PaymentClaimStatus? status, CancellationToken cancellationToken)
    {
        var claims = await _db.PaymentClaims
            .AsNoTracking()
            .Where(c => status == null || c.Status == status)
            .Select(c => new { Claim = c, CustomerName = c.Customer!.Name, MethodLabel = c.PaymentMethod == null ? null : c.PaymentMethod.Label })
            .ToListAsync(cancellationToken);
        var outstanding = await OutstandingAsync(claims.Select(c => c.Claim.CustomerId).Distinct().ToList(), cancellationToken);

        // Newest first, sorted in memory (SQLite in the tests can't order by created_at).
        return claims
            .OrderByDescending(c => c.Claim.CreatedAt).ThenByDescending(c => c.Claim.Id)
            .Select(c => ToDto(c.Claim, c.CustomerName, c.MethodLabel, outstanding.GetValueOrDefault(c.Claim.CustomerId)))
            .ToList();
    }

    public async Task<PaymentClaimDto> ConfirmAsync(Guid id, ConfirmClaimRequest request, CancellationToken cancellationToken)
    {
        var claim = await PendingAsync(id, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Leave Pending first, atomically: of two confirms at once (a double
        // click, both owners), only one moves the row; the other gets a 409
        // and settles nothing. The settle below then runs once.
        await ResolveAsync(id, PaymentClaimStatus.Confirmed, null, cancellationToken);

        // The settle refuses (409) more than the tab owes now; the transaction
        // then rolls back, and the claim is Pending again.
        await _payments.SettleInTransactionAsync(claim.CustomerId, new PaymentRequest
        {
            Amount = claim.Amount,
            Channel = request.Channel ?? claim.Channel,
            PaidOn = request.PaidOn ?? _clock.Today,
            Reference = claim.Reference,
            Notes = "reported on the tab page"
        }, claim.Id, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<PaymentClaimDto> RejectAsync(Guid id, RejectClaimRequest request, CancellationToken cancellationToken)
    {
        await PendingAsync(id, cancellationToken);
        await ResolveAsync(id, PaymentClaimStatus.Rejected, request.Reason.Trim(), cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>The claim as it stands (untracked), or 404, or 409 when it was already resolved.</summary>
    private async Task<PaymentClaim> PendingAsync(Guid id, CancellationToken cancellationToken)
    {
        var claim = await _db.PaymentClaims.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Payment claim", id);
        if (claim.Status != PaymentClaimStatus.Pending)
        {
            throw new ConflictException($"This claim was already {claim.Status.ToString().ToLowerInvariant()}.");
        }

        return claim;
    }

    /// <summary>
    /// Pending → resolved as one conditional UPDATE. Postgres makes a second
    /// concurrent update wait for the first, then re-checks "still Pending",
    /// finds it isn't, and changes nothing: that one gets the 409.
    /// </summary>
    private async Task ResolveAsync(Guid id, PaymentClaimStatus status, string? reason, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var user = _user.UserId;
        var moved = await _db.PaymentClaims
            .Where(c => c.Id == id && c.Status == PaymentClaimStatus.Pending)
            .ExecuteUpdateAsync(set => set
                .SetProperty(c => c.Status, status)
                .SetProperty(c => c.RejectReason, reason)
                .SetProperty(c => c.ResolvedAt, now)
                .SetProperty(c => c.ResolvedBy, user)
                .SetProperty(c => c.UpdatedAt, now), cancellationToken);
        if (moved == 0)
        {
            throw new ConflictException("Someone else just handled this claim. Refresh to see it.");
        }
    }

    private async Task<PaymentClaimDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        (await ListAsync(null, cancellationToken)).Single(c => c.Id == id);

    /// <summary>What each customer's tab owes now: the orders they pay for, less payments.</summary>
    private async Task<Dictionary<Guid, decimal>> OutstandingAsync(List<Guid> customerIds, CancellationToken cancellationToken)
    {
        var orders = await _db.Orders
            .AsNoTracking()
            .Where(o => customerIds.Contains(o.PayerId ?? o.CustomerId))
            .Select(o => new { o.Id, Payer = o.PayerId ?? o.CustomerId, o.Total })
            .ToListAsync(cancellationToken);
        var paid = await _db.PaidByOrderAsync(orders.Select(o => o.Id).ToList(), cancellationToken);
        return orders.GroupBy(o => o.Payer).ToDictionary(g => g.Key, g => g.Sum(o => o.Total - paid.GetValueOrDefault(o.Id)));
    }

    private static PaymentClaimDto ToDto(PaymentClaim c, string customerName, string? methodLabel, decimal outstanding) => new(
        c.Id, c.CustomerId, customerName, c.Amount, c.Channel, methodLabel, c.Reference, c.Status,
        c.CreatedAt, c.ResolvedAt, c.RejectReason, outstanding);
}

public interface IPaymentMethodService
{
    Task<IReadOnlyList<PaymentMethodDto>> ListAsync(CancellationToken cancellationToken);
    Task<PaymentMethodDto> CreateAsync(PaymentMethodRequest request, CancellationToken cancellationToken);
    Task<PaymentMethodDto> UpdateAsync(Guid id, PaymentMethodRequest request, CancellationToken cancellationToken);
    Task<PaymentMethodDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>The accounts customers can pay into from their tab page (GCash, CIMB Bank…), in the owners' order.</summary>
public class PaymentMethodService : IPaymentMethodService
{
    private readonly KloweeDbContext _db;

    public PaymentMethodService(KloweeDbContext db) => _db = db;

    public async Task<IReadOnlyList<PaymentMethodDto>> ListAsync(CancellationToken cancellationToken) =>
        (await _db.PaymentMethods.AsNoTracking().ToListAsync(cancellationToken))
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Label)
            .Select(ToDto)
            .ToList();

    public async Task<PaymentMethodDto> CreateAsync(PaymentMethodRequest request, CancellationToken cancellationToken)
    {
        var method = new PaymentMethod();
        Apply(method, request);
        _db.PaymentMethods.Add(method);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(method);
    }

    public async Task<PaymentMethodDto> UpdateAsync(Guid id, PaymentMethodRequest request, CancellationToken cancellationToken)
    {
        var method = await LoadAsync(id, cancellationToken);
        Apply(method, request);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(method);
    }

    public async Task<PaymentMethodDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var method = await LoadAsync(id, cancellationToken);
        method.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(method);
    }

    /// <summary>Soft delete; claims made with it keep their channel and still read.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var method = await LoadAsync(id, cancellationToken);
        method.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(PaymentMethod method, PaymentMethodRequest request)
    {
        method.Channel = request.Channel;
        method.Label = request.Label.Trim();
        method.AccountName = request.AccountName.Trim();
        method.AccountNumber = string.IsNullOrWhiteSpace(request.AccountNumber) ? null : request.AccountNumber.Trim();
        method.QrUrl = string.IsNullOrWhiteSpace(request.QrUrl) ? null : request.QrUrl.Trim();
        method.IsActive = request.IsActive;
        method.SortOrder = request.SortOrder;
    }

    private async Task<PaymentMethod> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.PaymentMethods.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
        ?? throw NotFoundException.For("Payment method", id);

    private static PaymentMethodDto ToDto(PaymentMethod m) =>
        new(m.Id, m.Channel, m.Label, m.AccountName, m.AccountNumber, m.QrUrl, m.IsActive, m.SortOrder);
}

public interface ICardRequestService
{
    Task<IReadOnlyList<CardRequestDto>> ListAsync(CancellationToken cancellationToken);
    Task ApproveAsync(Guid membershipId, CancellationToken cancellationToken);
    Task DeclineAsync(Guid membershipId, CancellationToken cancellationToken);
}

/// <summary>Cards customers asked for on their tab page (handover 17), waiting for the owners.</summary>
public class CardRequestService : ICardRequestService
{
    private readonly KloweeDbContext _db;
    private readonly IClock _clock;

    public CardRequestService(KloweeDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CardRequestDto>> ListAsync(CancellationToken cancellationToken) =>
        (await _db.LoyaltyMemberships
            .AsNoTracking()
            .Where(m => m.Status == MembershipStatus.Requested)
            .Select(m => new CardRequestDto(m.Id, m.CustomerId, m.Customer!.Name, m.ClubId, m.Club!.Name, m.CreatedAt))
            .ToListAsync(cancellationToken))
        .OrderBy(r => r.RequestedAt)
        .ToList();

    /// <summary>Gives the card: Active from today, and any card that comes with it (handover 16a).</summary>
    public async Task ApproveAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        var request = await RequestedAsync(membershipId, cancellationToken);
        var club = await _db.LoyaltyClubs.FirstAsync(c => c.Id == request.ClubId, cancellationToken);
        if (!club.IsActive)
        {
            throw new ConflictException($"{club.Name} isn't active. Activate it first.");
        }

        await LoyaltyMemberships.GiveAsync(_db, request.CustomerId, club, _clock.Today, null, cancellationToken);
    }

    public async Task DeclineAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        var request = await RequestedAsync(membershipId, cancellationToken);
        request.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<LoyaltyMembership> RequestedAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.LoyaltyMemberships.FirstOrDefaultAsync(m => m.Id == id && m.Status == MembershipStatus.Requested, cancellationToken)
        ?? throw NotFoundException.For("Card request", id);
}
