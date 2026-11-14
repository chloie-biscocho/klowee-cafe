using System.Globalization;
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
            var matcha = Club(LoyaltyClub.MatchaClubId, "The Matcha Club", LoyaltyClubStyle.Matcha, isActive: false, sortOrder: 2, shot);
            matcha.AutoJoinFromClubId = LoyaltyClub.CoffeeClubId;   // Coffee cardholders get it (handover 16a)
            db.LoyaltyClubs.Add(matcha);
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

/// <summary>One row of the owner-reviewed cardholder list (docs/import/loyalty-cardholders.csv).</summary>
/// <param name="CardNumber">Which Coffee Club card they are on now, 1 for the first. Blank or 0 in the file means 1.</param>
/// <param name="Position">The owner's count of filled slots (0–11), when given.</param>
/// <param name="Estimate">The scan's estimate, used when Position is blank.</param>
public sealed record CardholderListRow(int Line, string Customer, bool HasCard, int CardNumber, int? Position, int? Estimate)
{
    /// <summary>The slots filled on the current card: the owner's number, else the estimate, else 0.</summary>
    public int Slots => Position ?? Estimate ?? 0;
}

/// <summary>
/// Reads the cardholder list the owner edited in a spreadsheet: columns by
/// header name, quoted fields, Windows line endings, blank rows and extra
/// columns all fine. Anything it can't read is an error naming the line.
/// </summary>
public static class CardholderList
{
    public const string FileName = "loyalty-cardholders.csv";

    public static IReadOnlyList<CardholderListRow> Parse(string text)
    {
        var rows = ReadCsv(text);
        if (rows.Count == 0)
        {
            throw new InvalidDataException("The cardholder list is empty.");
        }

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Column(string name) => header.IndexOf(name) is var i and >= 0
            ? i
            : throw new InvalidDataException($"The cardholder list has no \"{name}\" column.");
        var (customer, hasCard, position, estimate) = (Column("customer"), Column("has_card"), Column("position"), Column("estimate"));
        var cardNumber = header.IndexOf("card_number");

        var result = new List<CardholderListRow>();
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            string Cell(int index) => index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;
            var line = i + 1;
            var name = Cell(customer);
            if (name.Length == 0)
            {
                continue;   // a blank row a spreadsheet left behind
            }

            var yes = Cell(hasCard).ToLowerInvariant() switch
            {
                "yes" or "y" => true,
                "no" or "n" => false,
                var other => throw new InvalidDataException($"Line {line} ({name}): has_card is \"{other}\"; use yes or no."),
            };

            var card = Number(Cell(cardNumber), line, name, "card_number", 0, 100) ?? 0;
            result.Add(new CardholderListRow(
                line, name, yes,
                card == 0 ? 1 : card,   // blank or 0: not set, so the card they are on is their first
                Number(Cell(position), line, name, "position", 0, 11),
                Number(Cell(estimate), line, name, "estimate", 0, 11)));
        }

        return result;
    }

    private static int? Number(string cell, int line, string name, string column, int min, int max)
    {
        if (cell.Length == 0)
        {
            return null;
        }

        if (!int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < min || value > max)
        {
            throw new InvalidDataException($"Line {line} ({name}): {column} is \"{cell}\"; use a whole number from {min} to {max}, or leave it blank.");
        }

        return value;
    }

    /// <summary>RFC 4180: quoted fields, doubled quotes, commas and line breaks inside quotes.</summary>
    private static List<List<string>> ReadCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                rows.Add(row);
                row = [];
            }
            else field.Append(c);
        }

        row.Add(field.ToString());
        if (row.Any(f => f.Length > 0)) rows.Add(row);
        return rows;
    }
}

/// <param name="Card">Which card they are on now (1 = first).</param>
/// <param name="Position">Slots filled on that card.</param>
/// <param name="Stamps">The adjustment written: 12 × (card − 1) + position.</param>
/// <param name="RewardsMarkedUsed">Every reward on earlier cards, and this card's up to the position.</param>
public sealed record CarriedCardholder(Guid CustomerId, string Name, int Card, int Position, int Stamps, int RewardsMarkedUsed);

public sealed record CarryOverResult(
    bool AlreadyApplied,
    string? Skipped,
    IReadOnlyList<string> CategoriesMapped,
    IReadOnlyList<string> NamesNotFound,
    int EntriesVoided,
    IReadOnlyList<CarriedCardholder> Cardholders)
{
    public string Summary()
    {
        if (Skipped is not null)
        {
            return Skipped;
        }

        if (AlreadyApplied)
        {
            return "Loyalty carry-over already applied; nothing to do.";
        }

        var s = new StringBuilder();
        s.Append($"Loyalty carry-over: mapped {(CategoriesMapped.Count == 0 ? "nothing" : string.Join(", ", CategoriesMapped))}. ");
        s.Append($"Not found (skipped): {(NamesNotFound.Count == 0 ? "none" : string.Join(", ", NamesNotFound))}. ");
        s.Append($"Old entries voided: {EntriesVoided}. Cardholders: {Cardholders.Count}, ");
        s.Append($"stamps carried: {Cardholders.Sum(c => c.Stamps)}, rewards marked used: {Cardholders.Sum(c => c.RewardsMarkedUsed)}.");
        return s.ToString();
    }
}

/// <summary>
/// Moves the paper Coffee Club cards into the system (decision 020, handover
/// 16a), once, from the owner-reviewed cardholder list:
///   1. makes sure both clubs exist (the migration created them; see LoyaltyClubDefaults);
///   2. maps categories by name, and Dirty Matcha to The Matcha Club;
///   3. voids every old single-card entry (kept as history, marked carried
///      over, so those orders never stamp again);
///   4. for each list row with has_card = yes: an Active Coffee Club card
///      "since the beginning" (null joined_on), one Adjustment of
///      12 × (card − 1) + position, and every reward already passed marked
///      used: all of them on earlier cards, and this card's up to the position.
/// Everyone else gets no card and nothing is written. Without the list it
/// doesn't run at all (and says so). It runs at startup behind a marker row,
/// in one transaction, and is idempotent without the marker too. Orders and
/// payments are never touched.
/// </summary>
public class LoyaltyCarryOver
{
    public const string MarkerName = "loyalty-clubs-carry-over";
    public const string AdjustmentNote = "carried over from paper card";
    public const string RedemptionNote = "carried over";

    /// <summary>The first run's note (handover 16): the Development redo recognises its rows.</summary>
    public const string OldAdjustmentNote = "carried over from the old card";

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
    private readonly string _listPath;

    /// <param name="listPath">The cardholder list; the copy published with the API by default.</param>
    public LoyaltyCarryOver(KloweeDbContext db, string? listPath = null)
    {
        _db = db;
        _listPath = listPath ?? Path.Combine(AppContext.BaseDirectory, "Data", CardholderList.FileName);
    }

    public async Task<CarryOverResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.DataMigrations.AnyAsync(m => m.Name == MarkerName, cancellationToken))
        {
            return new CarryOverResult(true, null, [], [], 0, []);
        }

        if (!File.Exists(_listPath))
        {
            return new CarryOverResult(false,
                $"WARNING: loyalty carry-over not run: the cardholder list {_listPath} is missing. " +
                "No customer has a Coffee Club card until it runs; it tries again at the next start.",
                [], [], 0, []);
        }

        var list = CardholderList.Parse(await File.ReadAllTextAsync(_listPath, cancellationToken));

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        await LoyaltyClubDefaults.EnsureAsync(_db, cancellationToken);
        var (mapped, notFound) = await MapAsync(cancellationToken);
        var voided = await VoidOldEntriesAsync(cancellationToken);
        var (cardholders, unknown) = await CarryCardholdersAsync(list, cancellationToken);
        notFound.AddRange(unknown);

        var result = new CarryOverResult(false, null, mapped, notFound, voided, cardholders);
        _db.DataMigrations.Add(new DataMigration { Name = MarkerName, Summary = result.Summary() });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    /// <summary>
    /// Development only: undoes an earlier carry-over (handover 16's rule or
    /// this one) so <see cref="RunAsync"/> can run again: its adjustments,
    /// redemptions and since-the-beginning cards removed, the old entries it
    /// voided restored, the marker cleared. Mappings stay.
    /// </summary>
    public async Task<int> UndoAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var removed = 0;

        var adjustments = await _db.LoyaltyEntries.IgnoreQueryFilters()
            .Where(e => e.Reason == LoyaltyReason.Adjustment && e.OrderId == null
                && (e.Notes == AdjustmentNote || e.Notes == OldAdjustmentNote))
            .ToListAsync(cancellationToken);
        _db.LoyaltyEntries.RemoveRange(adjustments);
        removed += adjustments.Count;

        var redemptions = await _db.LoyaltyRedemptions.IgnoreQueryFilters()
            .Where(r => r.OrderId == null && r.Note == RedemptionNote)
            .ToListAsync(cancellationToken);
        _db.LoyaltyRedemptions.RemoveRange(redemptions);
        removed += redemptions.Count;

        var cards = await _db.LoyaltyMemberships.IgnoreQueryFilters()
            .Where(m => m.Status == MembershipStatus.Active && m.JoinedOn == null)
            .ToListAsync(cancellationToken);
        _db.LoyaltyMemberships.RemoveRange(cards);
        removed += cards.Count;

        foreach (var entry in await _db.LoyaltyEntries.IgnoreQueryFilters().Where(e => e.CarriedOverAt != null).ToListAsync(cancellationToken))
        {
            entry.CarriedOverAt = null;
            entry.DeletedAt = null;
        }

        _db.DataMigrations.RemoveRange(await _db.DataMigrations.IgnoreQueryFilters().Where(m => m.Name == MarkerName).ToListAsync(cancellationToken));
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return removed;
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

    /// <summary>
    /// Every live entry from before the carry-over is the old single card's:
    /// voided, kept as history, and marked so its order never stamps again.
    /// </summary>
    private async Task<int> VoidOldEntriesAsync(CancellationToken cancellationToken)
    {
        // Done once: later entries are the clubs' own and must never be voided by a re-run.
        if (await _db.LoyaltyEntries.IgnoreQueryFilters().AnyAsync(e => e.CarriedOverAt != null, cancellationToken))
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        var old = await _db.LoyaltyEntries
            .Where(e => !(e.Reason == LoyaltyReason.Adjustment && e.Notes == AdjustmentNote))
            .ToListAsync(cancellationToken);
        foreach (var entry in old)
        {
            entry.DeletedAt = now;
            entry.CarriedOverAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return old.Count;
    }

    private async Task<(List<CarriedCardholder> Carried, List<string> NotFound)> CarryCardholdersAsync(
        IReadOnlyList<CardholderListRow> list,
        CancellationToken cancellationToken)
    {
        var coffee = await _db.LoyaltyClubs
            .Include(c => c.Rewards)
            .FirstAsync(c => c.Id == LoyaltyClub.CoffeeClubId, cancellationToken);
        var customers = await _db.Customers.Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken);
        var carried = new List<CarriedCardholder>();
        var notFound = new List<string>();

        foreach (var row in list.Where(r => r.HasCard))
        {
            var matches = customers.Where(c => string.Equals(c.Name, row.Customer, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1)
            {
                notFound.Add($"customer \"{row.Customer}\" ({(matches.Count == 0 ? "no match" : $"{matches.Count} matches")})");
                continue;
            }

            var customer = matches[0];
            // Already carried over: its adjustment, or its card "since the beginning" (a position-0 cardholder has no adjustment).
            if (await _db.LoyaltyEntries.AnyAsync(
                    e => e.CustomerId == customer.Id && e.Reason == LoyaltyReason.Adjustment && e.Notes == AdjustmentNote,
                    cancellationToken)
                || await _db.LoyaltyMemberships.AnyAsync(
                    m => m.CustomerId == customer.Id && m.ClubId == coffee.Id && m.Status == MembershipStatus.Active && m.JoinedOn == null,
                    cancellationToken))
            {
                continue;
            }

            if (!await _db.LoyaltyMemberships.AnyAsync(m => m.CustomerId == customer.Id && m.ClubId == coffee.Id, cancellationToken))
            {
                _db.LoyaltyMemberships.Add(new LoyaltyMembership
                {
                    CustomerId = customer.Id, ClubId = coffee.Id, Status = MembershipStatus.Active, JoinedOn = null,
                    Note = "paper card holder (carried over)"
                });
            }

            var position = row.Slots;
            var stamps = coffee.SlotCount * (row.CardNumber - 1) + position;
            if (stamps > 0)
            {
                _db.LoyaltyEntries.Add(new LoyaltyEntry
                {
                    CustomerId = customer.Id, ClubId = coffee.Id, Delta = stamps,
                    Reason = LoyaltyReason.Adjustment, Notes = AdjustmentNote
                });
            }

            // Already passed: every reward on the earlier cards, and this card's up to the position.
            var used = 0;
            for (var cycle = 0; cycle < row.CardNumber; cycle++)
            {
                var current = cycle == row.CardNumber - 1;
                foreach (var reward in coffee.Rewards.Where(r => !current || r.Slot <= position))
                {
                    _db.LoyaltyRedemptions.Add(new LoyaltyRedemption
                    {
                        CustomerId = customer.Id, ClubId = coffee.Id, RewardId = reward.Id, Cycle = cycle, Note = RedemptionNote
                    });
                    used++;
                }
            }

            carried.Add(new CarriedCardholder(customer.Id, customer.Name, row.CardNumber, position, stamps, used));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (carried, notFound);
    }
}
