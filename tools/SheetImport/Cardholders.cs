using System.Text.RegularExpressions;

namespace Klowee.SheetImport;

/// <summary>One order as the cardholder scan sees it.</summary>
/// <param name="Sequence">Order within the day: the import marker's sheet row, else the API's order.</param>
/// <param name="Notes">The order's notes with the "[import:…]" marker removed.</param>
/// <param name="CoffeeUnits">Drink units of this order that stamp The Coffee Club.</param>
/// <param name="ShotsAtZero">Extra-shot add-ons charged ₱0.</param>
/// <param name="LineRewards">Rewards applied to its lines under the clubs model (FreeDrink, FreeAddOn, PercentOff).</param>
public sealed record OrderFacts(
    string Customer,
    DateOnly On,
    int Sequence,
    string Notes,
    string DiscountType,
    decimal DiscountValue,
    bool Paid,
    int CoffeeUnits,
    int ShotsAtZero,
    IReadOnlyList<string> LineRewards);

/// <summary>A sign that an order used a Coffee Club card reward.</summary>
/// <param name="Slot">5 (50% off), 8 (free extra shot), 12 (free drink); 0 when the slot can't be told.</param>
/// <param name="Strong">False for wording that may not be a card reward ("50% off" alone, "stamp").</param>
public sealed record Evidence(int Slot, string Pattern, bool Strong);

/// <summary>One row of docs/import/loyalty-cardholders.csv.</summary>
public sealed record CardholderRow(
    string Customer,
    bool HasCard,
    int? Estimate,
    int? LastRewardSlot,
    DateOnly? LastRewardOn,
    int CoffeesPaidSince,
    string Evidence,
    string Flag);

/// <summary>
/// Who holds a paper Coffee Club card today (handover 16a, part 2): customers
/// whose orders show a card reward. Pure: the command feeds it orders read
/// from the API and writes the rows. The owner reviews the CSV before the
/// carry-over reads it.
/// </summary>
public static class Cardholders
{
    public const int Slots = 12;

    /// <summary>Accounts whose free drinks were the owners' own, not card rewards.</summary>
    public static readonly string[] InternalAccounts = ["Admin", "Office", "Kesha", "Chloie"];

    private static readonly Regex Marker = new(@"^\s*\[import:[^\]]*\]\s*", RegexOptions.Compiled);

    /// <summary>Note patterns, checked on every order, matched ignoring case. Each one's name goes into the evidence column.</summary>
    public static readonly IReadOnlyList<(string Name, Regex Pattern, int Slot, bool Strong)> NotePatterns =
    [
        ("100% loyalty / 100% off / free drink", new Regex(@"100\s*%\s*(loyalty|off)|free\s*drink", RegexOptions.IgnoreCase), 12, true),
        ("50% loyalty", new Regex(@"50\s*%\s*(off\s*)?(-\s*)?loyalty", RegexOptions.IgnoreCase), 5, true),
        ("50% without loyalty", new Regex(@"50\s*%(?!\s*(off\s*)?(-\s*)?loyalty)", RegexOptions.IgnoreCase), 5, false),
        ("free (extra) shot", new Regex(@"free\s*(extra\s*)?shot", RegexOptions.IgnoreCase), 8, true),
        ("stamp", new Regex(@"\bstamp", RegexOptions.IgnoreCase), 0, false),
    ];

    public static string StripMarker(string? notes) => Marker.Replace(notes ?? string.Empty, string.Empty).Trim();

    /// <summary>The import marker's sheet row ("[import:Sept 2025:85]" → 85), for ordering orders within a day.</summary>
    public static int? MarkerRow(string? notes)
    {
        var match = Regex.Match(notes ?? string.Empty, @"^\s*\[import:[^\]]*:(\d+)\]");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    /// <summary>Every sign of a card reward on one order.</summary>
    public static List<Evidence> Detect(OrderFacts order)
    {
        var found = new List<Evidence>();
        if (order.DiscountType == "LoyaltyFree")
        {
            found.Add(new(12, "LoyaltyFree discount", true));
        }

        if (order.DiscountType == "Percent" && order.DiscountValue == 50m)
        {
            // A 50% order discount is the card's slot 5 only when the notes say loyalty; alone it is weak.
            found.Add(new(5, "50% Percent discount", false));
        }

        foreach (var (name, pattern, slot, strong) in NotePatterns)
        {
            if (pattern.IsMatch(order.Notes))
            {
                found.Add(new(slot, $"notes \"{name}\"", strong));
            }
        }

        if (order.ShotsAtZero > 0)
        {
            found.Add(new(8, "extra shot at ₱0", true));
        }

        foreach (var reward in order.LineRewards)
        {
            found.Add(new(reward switch { "FreeDrink" => 12, "FreeAddOn" => 8, _ => 5 }, $"line reward {reward}", true));
        }

        return found;
    }

    /// <summary>The slot the order's reward sat on: its strongest sign, then the highest slot (a free drink with a free shot is slot 12).</summary>
    public static Evidence? Reward(IReadOnlyList<Evidence> found) =>
        found.Where(e => e.Slot > 0).OrderByDescending(e => e.Strong).ThenByDescending(e => e.Slot).FirstOrDefault();

    /// <summary>Where the card stands now: the reward's slot plus the coffees paid since, round a 12-slot card.</summary>
    public static int Estimate(int slot, int coffeesPaidSince) => (slot + coffeesPaidSince) % Slots;

    /// <summary>
    /// Whether the coffees since the last recorded reward went past another
    /// free drink (slot 12) with no record of it. A free drink itself completes
    /// the card, so after one it takes a whole new card.
    /// </summary>
    public static bool PassedFreeDrink(int slot, int coffeesPaidSince) =>
        slot == Slots ? coffeesPaidSince >= Slots : slot + coffeesPaidSince >= Slots;

    /// <summary>The CSV row for one customer from all their orders; null when nothing suggests a card.</summary>
    public static CardholderRow? Plan(string customer, IReadOnlyList<OrderFacts> orders)
    {
        var ordered = orders.OrderBy(o => o.On).ThenBy(o => o.Sequence).ToList();
        var withEvidence = ordered.Select(o => (Order: o, Found: Detect(o))).Where(x => x.Found.Count > 0).ToList();
        if (withEvidence.Count == 0)
        {
            return null;
        }

        var flags = new List<string>();
        var hasCard = true;
        var isSample = customer.EndsWith("(sample)", StringComparison.OrdinalIgnoreCase) || customer.Equals("Menu Test", StringComparison.OrdinalIgnoreCase);
        if (InternalAccounts.Any(a => a.Equals(customer, StringComparison.OrdinalIgnoreCase)))
        {
            flags.Add("owner or internal account: free drinks probably not card rewards");
            hasCard = false;
        }
        else if (isSample)
        {
            flags.Add("development sample data, not a real customer");
            hasCard = false;
        }

        var rewards = withEvidence.Where(x => Reward(x.Found) is not null).ToList();
        if (rewards.Count == 1 && rewards[0].Found.All(e => !e.Strong))
        {
            flags.Add("only evidence is one 50% without \"loyalty\" wording");
        }

        var evidence = string.Join("; ", withEvidence.Select(x =>
            $"{x.Order.On:yyyy-MM-dd} {string.Join(" + ", x.Found.Select(e => e.Pattern))}"));

        if (rewards.Count == 0)
        {
            flags.Add("no reward slot can be told from the wording");
            return new CardholderRow(customer, hasCard, null, null, null, 0, evidence, string.Join("; ", flags));
        }

        var (last, lastFound) = rewards[^1];
        var slot = Reward(lastFound)!.Slot;
        if (lastFound.All(e => !e.Strong) && rewards.Count > 1)
        {
            flags.Add("last reward rests on ambiguous wording");
        }

        // Coffee Club drinks paid after the reward's drink: the rest of that order, then every later paid order.
        var index = ordered.IndexOf(last);
        var since = (last.Paid ? Math.Max(0, last.CoffeeUnits - 1) : 0)
            + ordered.Skip(index + 1).Where(o => o.Paid).Sum(o => o.CoffeeUnits);
        if (PassedFreeDrink(slot, since))
        {
            flags.Add("passed slot 12 since the last recorded reward: a free drink may have gone unrecorded");
        }

        return new CardholderRow(customer, hasCard, Estimate(slot, since), slot, last.On, since, evidence, string.Join("; ", flags));
    }

    public static readonly string[] Header =
        ["customer", "has_card", "position", "estimate", "last_reward", "last_reward_on", "coffees_paid_since", "evidence", "flag"];

    public static IReadOnlyList<string> ToCsv(CardholderRow row) =>
    [
        row.Customer,
        row.HasCard ? "yes" : "no",
        "",
        row.Estimate?.ToString() ?? "",
        row.LastRewardSlot switch { 5 => "50% off (slot 5)", 8 => "free extra shot (slot 8)", 12 => "free drink (slot 12)", _ => "" },
        row.LastRewardOn?.ToString("yyyy-MM-dd") ?? "",
        row.CoffeesPaidSince.ToString(),
        row.Evidence,
        row.Flag,
    ];
}
