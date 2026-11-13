using System.Text;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

/// <summary>
/// The two clubs the LoyaltyClubs migration creates. The migration inserts
/// them in SQL (it runs in the deploy workflow, before this code is live);
/// this copy creates them where that migration never ran (the test suite's
/// SQLite, built with EnsureCreated). Keep the two in step.
/// </summary>
public static class LoyaltyClubDefaults
{
    public const string ExtraShotAddOn = "Extra shot";

    public static async Task EnsureAsync(KloweeDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.LoyaltyClubs.IgnoreQueryFilters().Select(c => c.Id).ToListAsync(cancellationToken);
        var shot = await db.AddOns
            .Where(a => a.Name.ToLower() == ExtraShotAddOn.ToLower())
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!existing.Contains(LoyaltyClub.CoffeeClubId))
        {
            db.LoyaltyClubs.Add(Club(LoyaltyClub.CoffeeClubId, "The Coffee Club", LoyaltyClubStyle.Coffee, isActive: true, sortOrder: 1, shot));
        }

        if (!existing.Contains(LoyaltyClub.MatchaClubId))
        {
            db.LoyaltyClubs.Add(Club(LoyaltyClub.MatchaClubId, "The Matcha Club", LoyaltyClubStyle.Matcha, isActive: false, sortOrder: 2, shot));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static LoyaltyClub Club(Guid id, string name, LoyaltyClubStyle style, bool isActive, int sortOrder, Guid? shot) => new()
    {
        Id = id,
        Name = name,
        Style = style,
        SlotCount = 12,
        IsActive = isActive,
        SortOrder = sortOrder,
        Rewards =
        {
            new LoyaltyClubReward { Slot = 5, Type = LoyaltyRewardType.PercentOff, Percent = 50m, Label = "50% OFF" },
            new LoyaltyClubReward { Slot = 8, Type = LoyaltyRewardType.FreeAddOn, AddOnId = shot, Label = "EXTRA SHOT" },
            new LoyaltyClubReward { Slot = 12, Type = LoyaltyRewardType.FreeDrink, Label = "FREE DRINK" }
        }
    };
}

/// <param name="OldBalance">The old card's balance: the sum of the customer's live entries.</param>
/// <param name="Carried">What moved to The Coffee Club: the old balance, floored at 0 and capped at 11.</param>
/// <param name="RewardsMarkedUsed">Coffee Club rewards on slots up to <paramref name="Carried"/>, already passed.</param>
public sealed record CarriedCustomer(Guid CustomerId, int OldBalance, int Carried, int RewardsMarkedUsed);

public sealed record CarryOverResult(
    bool AlreadyApplied,
    IReadOnlyList<string> CategoriesMapped,
    IReadOnlyList<string> NamesNotFound,
    int EntriesVoided,
    IReadOnlyList<CarriedCustomer> Customers)
{
    public string Summary()
    {
        if (AlreadyApplied)
        {
            return "Loyalty carry-over already applied; nothing to do.";
        }

        var s = new StringBuilder();
        s.Append($"Mapped: {(CategoriesMapped.Count == 0 ? "nothing" : string.Join(", ", CategoriesMapped))}. ");
        s.Append($"Not found (skipped): {(NamesNotFound.Count == 0 ? "none" : string.Join(", ", NamesNotFound))}. ");
        s.Append($"Customers carried over: {Customers.Count}, entries voided: {EntriesVoided}, ");
        s.Append($"stamps carried: {Customers.Sum(c => c.Carried)}, rewards marked used: {Customers.Sum(c => c.RewardsMarkedUsed)}.");
        return s.ToString();
    }
}

/// <summary>
/// Moves the old single stamp card onto The Coffee Club (decision 020), once:
///   1. makes sure both clubs exist (the migration created them; see LoyaltyClubDefaults);
///   2. maps categories by name, and Dirty Matcha to The Matcha Club;
///   3. per customer with live entries: old balance B (floor 0, cap 11), void
///      every old entry (kept as history, marked carried over), write one
///      Adjustment of +B, and mark every Coffee Club reward on a slot ≤ B used
///      on card 1, so nobody gets a reward twice.
/// Runs at startup behind a one-time marker row, in one transaction. It is
/// also idempotent without the marker: a customer already carried over is
/// skipped, and mappings only fill blanks. Orders and payments are never touched.
/// </summary>
public class LoyaltyCarryOver
{
    public const string MarkerName = "loyalty-clubs-carry-over";
    public const string AdjustmentNote = "carried over from the old card";
    public const string RedemptionNote = "carried over";

    /// <summary>Handover 16's mapping, matched by name ignoring case.</summary>
    public static readonly IReadOnlyList<(string Category, Guid Club)> CategoryClubs =
    [
        ("Coffee", LoyaltyClub.CoffeeClubId),
        ("Signature Coffee", LoyaltyClub.CoffeeClubId),
        ("Houseblend Matcha", LoyaltyClub.MatchaClubId),
        ("Okumidori Matcha", LoyaltyClub.MatchaClubId)
    ];

    /// <summary>Items that stamp another club than their category's.</summary>
    public static readonly IReadOnlyList<(string Item, Guid Club)> ItemClubs =
    [
        ("Dirty Matcha", LoyaltyClub.MatchaClubId)
    ];

    private readonly KloweeDbContext _db;

    public LoyaltyCarryOver(KloweeDbContext db) => _db = db;

    public async Task<CarryOverResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.DataMigrations.AnyAsync(m => m.Name == MarkerName, cancellationToken))
        {
            return new CarryOverResult(true, [], [], 0, []);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        await LoyaltyClubDefaults.EnsureAsync(_db, cancellationToken);
        var (mapped, notFound) = await MapAsync(cancellationToken);
        var (voided, customers) = await CarryOverAsync(cancellationToken);

        var result = new CarryOverResult(false, mapped, notFound, voided, customers);
        _db.DataMigrations.Add(new DataMigration { Name = MarkerName, Summary = result.Summary() });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    private async Task<(List<string> Mapped, List<string> NotFound)> MapAsync(CancellationToken cancellationToken)
    {
        var mapped = new List<string>();
        var notFound = new List<string>();
        var clubNames = await _db.LoyaltyClubs.ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var categories = await _db.MenuCategories.ToListAsync(cancellationToken);
        var items = await _db.MenuItems.ToListAsync(cancellationToken);

        foreach (var (name, club) in CategoryClubs)
        {
            var category = categories.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            if (category is null)
            {
                notFound.Add($"category \"{name}\"");
            }
            else if (category.LoyaltyClubId is null)
            {
                category.LoyaltyClubId = club;
                mapped.Add($"{category.Name} → {clubNames[club]}");
            }
        }

        foreach (var (name, club) in ItemClubs)
        {
            var item = items.FirstOrDefault(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                notFound.Add($"item \"{name}\"");
            }
            else if (item.LoyaltyMode == LoyaltyMode.Inherit)
            {
                item.LoyaltyMode = LoyaltyMode.Club;
                item.LoyaltyClubId = club;
                mapped.Add($"{item.Name} (item) → {clubNames[club]}");
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (mapped, notFound);
    }

    private async Task<(int Voided, List<CarriedCustomer> Customers)> CarryOverAsync(CancellationToken cancellationToken)
    {
        var coffee = await _db.LoyaltyClubs
            .Include(c => c.Rewards)
            .FirstAsync(c => c.Id == LoyaltyClub.CoffeeClubId, cancellationToken);
        var cap = coffee.SlotCount - 1;

        // Already carried over: an old entry was voided by this step, or the adjustment exists.
        var done = (await _db.LoyaltyEntries
                .IgnoreQueryFilters()
                .Where(e => e.CarriedOverAt != null
                    || (e.DeletedAt == null && e.Reason == LoyaltyReason.Adjustment && e.Notes == AdjustmentNote))
                .Select(e => e.CustomerId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var live = (await _db.LoyaltyEntries.ToListAsync(cancellationToken))
            .Where(e => !done.Contains(e.CustomerId))
            .GroupBy(e => e.CustomerId)
            .ToList();

        var now = DateTimeOffset.UtcNow;
        var voided = 0;
        var customers = new List<CarriedCustomer>();

        foreach (var entries in live)
        {
            var oldBalance = entries.Sum(e => e.Delta);
            var carried = Math.Clamp(oldBalance, 0, cap);

            foreach (var entry in entries)
            {
                entry.DeletedAt = now;
                entry.CarriedOverAt = now;
                voided++;
            }

            if (carried > 0)
            {
                _db.LoyaltyEntries.Add(new LoyaltyEntry
                {
                    CustomerId = entries.Key,
                    ClubId = coffee.Id,
                    Delta = carried,
                    Reason = LoyaltyReason.Adjustment,
                    Notes = AdjustmentNote
                });
            }

            var passed = coffee.Rewards.Where(r => r.Slot <= carried).ToList();
            foreach (var reward in passed)
            {
                _db.LoyaltyRedemptions.Add(new LoyaltyRedemption
                {
                    CustomerId = entries.Key,
                    ClubId = coffee.Id,
                    RewardId = reward.Id,
                    Cycle = 0,
                    Note = RedemptionNote
                });
            }

            customers.Add(new CarriedCustomer(entries.Key, oldBalance, carried, passed.Count));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (voided, customers);
    }
}
