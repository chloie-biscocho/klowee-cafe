using System.Text.RegularExpressions;
using Klowee.Api.Common;
using Klowee.Api.Contracts.Customers;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Contracts.Tabs;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Klowee.Api.Services;

public interface IPublicTabService
{
    /// <summary>The customer's page, or NotFound for any token that isn't a live link.</summary>
    Task<PublicTabDto> GetAsync(string token, CancellationToken cancellationToken);

    /// <summary>"I've paid": a Pending claim for the owners to confirm.</summary>
    Task<PublicClaimDto> ClaimAsync(string token, PublicClaimRequest request, CancellationToken cancellationToken);

    /// <summary>"Ask for a card": a Requested membership; nothing happens if they have one or asked already.</summary>
    Task RequestCardAsync(string token, Guid clubId, CancellationToken cancellationToken);
}

/// <summary>
/// The customer's private page (handover 17, decision 021). The token is the
/// only key: no login. The payload is what the customer may see and nothing
/// more: their tab (the orders they pay for, "c/o" ones included), their own
/// loyalty cards, recent payments and claims, and how to pay. No other
/// customer's name, no internal ids but claims', clubs' and payment methods',
/// no notes, no discount reasons. Tokens never reach a log; the customer id does.
/// </summary>
public partial class PublicTabService : IPublicTabService
{
    public const string NoteKey = "tab_page_note";

    private readonly KloweeDbContext _db;
    private readonly IOrderService _orders;
    private readonly ILoyaltyService _loyalty;
    private readonly TimeZoneInfo _zone;
    private readonly ILogger<PublicTabService> _logger;

    public PublicTabService(
        KloweeDbContext db,
        IOrderService orders,
        ILoyaltyService loyalty,
        IOptions<AppOptions> app,
        ILogger<PublicTabService> logger)
    {
        _db = db;
        _orders = orders;
        _loyalty = loyalty;
        _zone = TimeZoneInfo.FindSystemTimeZoneById(app.Value.TimeZone);
        _logger = logger;
    }

    [GeneratedRegex("^[A-Za-z0-9]{6,32}$")]
    private static partial Regex ReferencePattern();

    public async Task<PublicTabDto> GetAsync(string token, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(token, cancellationToken);
        _logger.LogInformation("Tab page read for customer {CustomerId}", customer.Id);

        var tab = await TabAsync(customer.Id, cancellationToken);
        var claims = await _db.PaymentClaims
            .AsNoTracking()
            .Where(c => c.CustomerId == customer.Id)
            .ToListAsync(cancellationToken);
        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p => tab.Select(o => o.Id).Contains(p.OrderId))
            .Select(p => new { p.PaidOn, p.Channel, p.Amount, p.CreatedAt })
            .ToListAsync(cancellationToken);

        var methods = await _db.PaymentMethods.AsNoTracking().Where(m => m.IsActive).ToListAsync(cancellationToken);
        var note = await _db.SiteSettings.AsNoTracking().Where(s => s.Key == NoteKey).Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);

        return new PublicTabDto(
            customer.Nickname ?? customer.Name,
            tab.Sum(o => o.Total - o.Paid),
            claims.Where(c => c.Status == PaymentClaimStatus.Pending).Sum(c => c.Amount),
            tab.Where(o => o.Total > o.Paid)
                .OrderBy(o => o.OrderedOn)
                .Select(o => new PublicOrderDto(o.OrderedOn, o.Items, o.Total, o.Paid, o.Total - o.Paid))
                .ToList(),
            payments.OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.CreatedAt).Take(5)
                .Select(p => new PublicPaymentDto(p.PaidOn, p.Channel, p.Amount))
                .ToList(),
            claims.OrderByDescending(c => c.CreatedAt).Take(5)
                .Select(c => new PublicClaimDto(c.Id, c.Amount, c.Reference, c.Status, LocalDate(c.CreatedAt), c.Channel, c.RejectReason))
                .ToList(),
            await ClubsAsync(customer.Id, cancellationToken),
            new PublicPayDto(
                string.IsNullOrWhiteSpace(note) ? null : note,
                methods.OrderBy(m => m.SortOrder).ThenBy(m => m.Label)
                    .Select(m => new PublicPayMethodDto(m.Id, m.Channel, m.Label, m.AccountName, m.AccountNumber, m.QrUrl))
                    .ToList()));
    }

    public async Task<PublicClaimDto> ClaimAsync(string token, PublicClaimRequest request, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(token, cancellationToken);
        var reference = request.Reference.Trim();
        if (!ReferencePattern().IsMatch(reference))
        {
            throw new ValidationFailedException("The reference number is 6 to 32 letters and digits, as your app shows it.");
        }

        var method = await _db.PaymentMethods.FirstOrDefaultAsync(m => m.Id == request.PaymentMethodId && m.IsActive, cancellationToken)
            ?? throw new ConflictException("That way to pay isn't available any more. Please reload the page.");

        var owed = (await TabAsync(customer.Id, cancellationToken)).Sum(o => o.Total - o.Paid);
        var pending = (await _db.PaymentClaims
                .Where(c => c.CustomerId == customer.Id && c.Status == PaymentClaimStatus.Pending)
                .Select(c => c.Amount)
                .ToListAsync(cancellationToken))
            .Sum();
        var limit = owed - pending;
        if (request.Amount > limit)
        {
            throw new ConflictException(limit <= 0m
                ? "Your tab is already covered by what you've reported. Klowee will confirm it soon."
                : $"That's more than your tab: you can report up to ₱{limit:#,0.##}.");
        }

        var claim = new PaymentClaim
        {
            CustomerId = customer.Id,
            Amount = request.Amount,
            Channel = method.Channel,
            PaymentMethodId = method.Id,
            Reference = reference,
        };
        _db.PaymentClaims.Add(claim);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Payment claim {ClaimId} reported for customer {CustomerId}", claim.Id, customer.Id);

        return new PublicClaimDto(claim.Id, claim.Amount, claim.Reference, claim.Status, LocalDate(claim.CreatedAt), claim.Channel, null);
    }

    public async Task RequestCardAsync(string token, Guid clubId, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(token, cancellationToken);
        var club = await _db.LoyaltyClubs.FirstOrDefaultAsync(c => c.Id == clubId, cancellationToken)
            ?? throw NotFoundException.For("Loyalty card", clubId);

        if (!club.IsActive)
        {
            throw new ConflictException($"{club.Name} isn't running yet.");
        }

        if (club.AutoJoinFromClubId is not null)
        {
            throw new ConflictException($"{club.Name} comes with another card; ask for that one.");
        }

        // Already a member, or already asked: nothing to do.
        if (await _db.LoyaltyMemberships.AnyAsync(m => m.CustomerId == customer.Id && m.ClubId == clubId, cancellationToken))
        {
            return;
        }

        _db.LoyaltyMemberships.Add(new LoyaltyMembership
        {
            CustomerId = customer.Id, ClubId = clubId, Status = MembershipStatus.Requested, Note = "asked on their tab page"
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Card requested for customer {CustomerId}", customer.Id);
    }

    /// <summary>
    /// The customer behind a token. Any token that isn't a live link (malformed,
    /// replaced, removed) gets the same lookup and the same 404.
    /// </summary>
    private async Task<Customer> FindAsync(string token, CancellationToken cancellationToken) =>
        await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.TabToken == token, cancellationToken)
        ?? throw new NotFoundException("This link isn't active.");

    /// <summary>The orders this customer pays for (their own unless someone else pays, and those they cover).</summary>
    private async Task<IReadOnlyList<OrderSummaryDto>> TabAsync(Guid customerId, CancellationToken cancellationToken) =>
        await _orders.SummariesAsync(
            _db.Orders.AsNoTracking().Where(o => (o.PayerId ?? o.CustomerId) == customerId),
            cancellationToken);

    /// <summary>Every active club, from the customer's own cards (as drinker), without internal ids.</summary>
    private async Task<IReadOnlyList<PublicClubDto>> ClubsAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var cards = (await _loyalty.CardsAsync([customerId], cancellationToken))[customerId];
        var clubs = await _db.LoyaltyClubs.AsNoTracking().Include(c => c.Rewards).ToDictionaryAsync(c => c.Id, cancellationToken);
        var asked = await _db.LoyaltyMemberships
            .AsNoTracking()
            .Where(m => m.CustomerId == customerId && m.Status == MembershipStatus.Requested)
            .ToDictionaryAsync(m => m.ClubId, m => m.CreatedAt, cancellationToken);

        static PublicRewardDto Reward(CustomerRewardDto r) => new(r.Slot, r.Label, r.Type, r.State, r.Cycle);

        return cards.Select(c =>
        {
            var source = clubs[c.ClubId].AutoJoinFromClubId;
            return new PublicClubDto(
                c.ClubId, c.Name, c.Style, c.SlotCount, c.Membership,
                asked.TryGetValue(c.ClubId, out var on) ? LocalDate(on) : null,
                source,
                source is { } s && clubs.TryGetValue(s, out var from) ? from.Name : null,
                c.Position, c.Cycle,
                // A member's rewards and their states; without a card, the
                // club's printed rewards, so the dimmed card shows what it gives.
                c.Membership == "member"
                    ? c.Rewards.Select(Reward).ToList()
                    : clubs[c.ClubId].Rewards.OrderBy(r => r.Slot)
                        .Select(r => new PublicRewardDto(r.Slot, r.Label, r.Type, "locked", 0)).ToList(),
                c.PendingDrinks,
                c.Cards.Select(card => new PublicCardDto(
                    card.CardNumber,
                    card.IsCurrent,
                    card.Slots.Select(slot => new PublicSlotDto(slot.Slot, slot.Filled, slot.Date, slot.Source)).ToList(),
                    card.Rewards.Select(Reward).ToList())).ToList());
        }).ToList();
    }

    private DateOnly LocalDate(DateTimeOffset at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, _zone).DateTime);
}
