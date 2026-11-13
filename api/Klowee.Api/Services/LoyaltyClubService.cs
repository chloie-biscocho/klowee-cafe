using Klowee.Api.Common;
using Klowee.Api.Contracts.Loyalty;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

public interface ILoyaltyClubService
{
    Task<IReadOnlyList<LoyaltyClubDto>> ListAsync(CancellationToken cancellationToken);
    Task<LoyaltyClubDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<LoyaltyClubDto> CreateAsync(LoyaltyClubRequest request, CancellationToken cancellationToken);
    Task<LoyaltyClubDto> UpdateAsync(Guid id, LoyaltyClubRequest request, CancellationToken cancellationToken);

    /// <summary>Idempotent. Activation sets activated_on to today (business time zone) the first time.</summary>
    Task<LoyaltyClubDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);

    /// <summary>Refused (409) once the club has any ledger entry: deactivate it instead.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>The loyalty cards themselves: name, look, slots, rewards, and which categories feed them.</summary>
public class LoyaltyClubService : ILoyaltyClubService
{
    private readonly KloweeDbContext _db;
    private readonly IClock _clock;

    public LoyaltyClubService(KloweeDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<LoyaltyClubDto>> ListAsync(CancellationToken cancellationToken)
    {
        var clubs = await _db.LoyaltyClubs.AsNoTracking().Include(c => c.Rewards).ToListAsync(cancellationToken);
        return await ToDtosAsync(clubs.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList(), cancellationToken);
    }

    public async Task<LoyaltyClubDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var club = await _db.LoyaltyClubs.AsNoTracking().Include(c => c.Rewards).FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Loyalty club", id);
        return (await ToDtosAsync([club], cancellationToken))[0];
    }

    public async Task<LoyaltyClubDto> CreateAsync(LoyaltyClubRequest request, CancellationToken cancellationToken)
    {
        var club = new LoyaltyClub();
        await ApplyAsync(club, request, cancellationToken);
        _db.LoyaltyClubs.Add(club);
        await _db.SaveChangesAsync(cancellationToken);
        await ApplyCategoriesAsync(club.Id, request.CategoryIds, cancellationToken);
        return await GetAsync(club.Id, cancellationToken);
    }

    public async Task<LoyaltyClubDto> UpdateAsync(Guid id, LoyaltyClubRequest request, CancellationToken cancellationToken)
    {
        var club = await LoadAsync(id, cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        await ApplyAsync(club, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await ApplyCategoriesAsync(club.Id, request.CategoryIds, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<LoyaltyClubDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var club = await LoadAsync(id, cancellationToken);
        Activate(club, isActive);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var club = await LoadAsync(id, cancellationToken);

        // Voided entries count too: they are the club's history.
        if (await _db.LoyaltyEntries.IgnoreQueryFilters().AnyAsync(e => e.ClubId == id, cancellationToken))
        {
            throw new ConflictException($"{club.Name} has stamps on its ledger, so it can't be deleted. Deactivate it instead.");
        }

        var now = DateTimeOffset.UtcNow;
        club.DeletedAt = now;
        foreach (var reward in club.Rewards)
        {
            reward.DeletedAt = now;
        }

        await ApplyCategoriesAsync(id, [], cancellationToken);
        foreach (var item in await _db.MenuItems.Where(i => i.LoyaltyClubId == id).ToListAsync(cancellationToken))
        {
            item.LoyaltyMode = LoyaltyMode.Inherit;
            item.LoyaltyClubId = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Validates the request and writes it onto the club. Rewards are replaced
    /// in full but matched by slot: a slot that stays keeps its reward row (and
    /// so its uses), a slot dropped is removed, a new slot is added.
    /// </summary>
    private async Task ApplyAsync(LoyaltyClub club, LoyaltyClubRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _db.LoyaltyClubs.AnyAsync(c => c.Name.ToLower() == name.ToLower() && c.Id != club.Id, cancellationToken))
        {
            throw new ConflictException($"A loyalty card named '{name}' already exists.");
        }

        var duplicate = request.Rewards.GroupBy(r => r.Slot).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ValidationFailedException($"Slot {duplicate.Key} has more than one reward.");
        }

        var addOnIds = request.Rewards.Where(r => r.AddOnId is not null).Select(r => r.AddOnId!.Value).Distinct().ToList();
        var addOns = await _db.AddOns.Where(a => addOnIds.Contains(a.Id)).Select(a => a.Id).ToListAsync(cancellationToken);

        foreach (var reward in request.Rewards)
        {
            if (reward.Slot > request.SlotCount)
            {
                throw new ValidationFailedException($"Slot {reward.Slot} is past the card's {request.SlotCount} slots.");
            }

            if (string.IsNullOrWhiteSpace(reward.Label))
            {
                throw new ValidationFailedException($"The reward on slot {reward.Slot} needs a label.");
            }

            switch (reward.Type)
            {
                case LoyaltyRewardType.PercentOff when reward.Percent is not (> 0m and <= 100m):
                    throw new ValidationFailedException($"The reward on slot {reward.Slot} needs a percent from 1 to 100.");
                case LoyaltyRewardType.FreeAddOn when reward.AddOnId is null:
                    throw new ValidationFailedException($"The reward on slot {reward.Slot} needs an add-on.");
                case LoyaltyRewardType.FreeAddOn when !addOns.Contains(reward.AddOnId!.Value):
                    throw new ValidationFailedException($"Add-on '{reward.AddOnId}' was not found.");
            }
        }

        club.Name = name;
        club.Style = request.Style;
        club.SlotCount = request.SlotCount;
        club.SortOrder = request.SortOrder;
        Activate(club, request.IsActive);

        var now = DateTimeOffset.UtcNow;
        var current = club.Rewards.Where(r => r.DeletedAt is null).ToList();
        foreach (var gone in current.Where(r => request.Rewards.All(n => n.Slot != r.Slot)))
        {
            gone.DeletedAt = now;
        }

        foreach (var wanted in request.Rewards)
        {
            var reward = current.FirstOrDefault(r => r.Slot == wanted.Slot);
            if (reward is null)
            {
                reward = new LoyaltyClubReward { Slot = wanted.Slot };
                club.Rewards.Add(reward);
            }

            reward.Type = wanted.Type;
            reward.Percent = wanted.Type == LoyaltyRewardType.PercentOff ? wanted.Percent : null;
            reward.AddOnId = wanted.Type == LoyaltyRewardType.FreeAddOn ? wanted.AddOnId : null;
            reward.Label = wanted.Label.Trim();
        }
    }

    private void Activate(LoyaltyClub club, bool isActive)
    {
        club.IsActive = isActive;
        if (isActive && club.ActivatedOn is null)
        {
            club.ActivatedOn = _clock.Today;
        }
    }

    /// <summary>When sent, exactly these categories feed the club; others it had are left without a club.</summary>
    private async Task ApplyCategoriesAsync(Guid clubId, IReadOnlyCollection<Guid>? categoryIds, CancellationToken cancellationToken)
    {
        if (categoryIds is null)
        {
            return;
        }

        var wanted = categoryIds.Distinct().ToList();
        var found = await _db.MenuCategories.Where(c => wanted.Contains(c.Id)).ToListAsync(cancellationToken);
        var missing = wanted.Except(found.Select(c => c.Id)).ToList();
        if (missing.Count > 0)
        {
            throw new ValidationFailedException($"Menu categor(ies) not found: {string.Join(", ", missing)}.");
        }

        foreach (var category in found)
        {
            category.LoyaltyClubId = clubId;
        }

        foreach (var category in await _db.MenuCategories.Where(c => c.LoyaltyClubId == clubId && !wanted.Contains(c.Id)).ToListAsync(cancellationToken))
        {
            category.LoyaltyClubId = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<LoyaltyClubDto>> ToDtosAsync(List<LoyaltyClub> clubs, CancellationToken cancellationToken)
    {
        var ids = clubs.Select(c => c.Id).ToList();
        var categories = await _db.MenuCategories
            .AsNoTracking()
            .Where(c => c.LoyaltyClubId != null && ids.Contains(c.LoyaltyClubId.Value))
            .Select(c => new { c.Id, ClubId = c.LoyaltyClubId!.Value, c.SortOrder, c.Name })
            .ToListAsync(cancellationToken);
        var members = (await _db.LoyaltyEntries
                .AsNoTracking()
                .Where(e => ids.Contains(e.ClubId))
                .Select(e => new { e.ClubId, e.CustomerId })
                .Distinct()
                .ToListAsync(cancellationToken))
            .GroupBy(e => e.ClubId)
            .ToDictionary(g => g.Key, g => g.Count());
        var addOnNames = await _db.AddOns.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        return clubs.Select(c => new LoyaltyClubDto(
            c.Id, c.Name, c.Style, c.SlotCount, c.IsActive, c.ActivatedOn, c.SortOrder,
            c.Rewards.Where(r => r.DeletedAt is null).OrderBy(r => r.Slot)
                .Select(r => new LoyaltyClubRewardDto(
                    r.Id, r.Slot, r.Type, r.Percent, r.AddOnId,
                    r.AddOnId is { } a ? addOnNames.GetValueOrDefault(a) : null, r.Label))
                .ToList(),
            categories.Where(x => x.ClubId == c.Id).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => x.Id).ToList(),
            members.GetValueOrDefault(c.Id))).ToList();
    }

    private async Task<LoyaltyClub> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.LoyaltyClubs.Include(c => c.Rewards).FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
        ?? throw NotFoundException.For("Loyalty club", id);
}
