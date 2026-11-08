using System.Globalization;
using System.Text.RegularExpressions;

namespace Klowee.SheetImport;

/// <summary>A sheet row interpreted: who, what, how much, how it was paid, and anything doubtful.</summary>
public sealed record ParsedRow
{
    public required string Tab { get; init; }
    public required int Row { get; init; }
    public required DateOnly Date { get; init; }
    public required string RawName { get; init; }
    /// <summary>The drinker, with any "c/o X" removed.</summary>
    public required string Name { get; init; }
    /// <summary>Who paid, from "c/o X" in the name or notes; empty when the drinker paid.</summary>
    public required string Payer { get; init; }
    public required string RawDrink { get; init; }
    /// <summary>The drink with any quantity ("x4", "13 drinks") removed; the key into drinks.csv.</summary>
    public required string Drink { get; init; }
    public required int Quantity { get; init; }
    /// <summary>The sheet's price divided by the quantity.</summary>
    public required decimal UnitPrice { get; init; }
    public required decimal ExtraShot { get; init; }
    public required decimal OatMilk { get; init; }
    /// <summary>The sheet's Total, which is what was charged.</summary>
    public required decimal Total { get; init; }
    /// <summary>Paid, Unpaid, Awaiting, or empty (May 2025 has no status column).</summary>
    public required string Status { get; init; }
    public required string RawChannel { get; init; }
    /// <summary>The API channel (Cash, GCash, BusinessGCash, CIMB, BPI, Other), or AR / Free / empty.</summary>
    public required string Channel { get; init; }
    /// <summary>None, Percent, Fixed or Free, read from the notes and the channel.</summary>
    public required string Discount { get; init; }
    public required decimal DiscountValue { get; init; }
    public required DateOnly? PaidOn { get; init; }
    public required string RawPaid { get; init; }
    public required string Notes { get; init; }
    public required List<string> Flags { get; init; }
    /// <summary>Why the row can't become an order at all; empty when it can.</summary>
    public required string Skip { get; init; }

    public decimal Gross => UnitPrice * Quantity + ExtraShot + OatMilk;
}

/// <summary>The parsing rules, one function per column family, all pure.</summary>
public static partial class Rows
{
    public static ParsedRow Parse(SheetRow s)
    {
        var flags = new List<string>();

        var (drink, quantity) = SplitQuantity(s.Drink, s.Name);
        var (name, payer) = SplitCareOf(TimesN().Replace(s.Name, "").Trim(), s.Notes, flags);
        var price = Money(s.Price, "price", flags);
        var total = Money(s.Total, "total", flags);
        var shot = Money(s.ExtraShot, "extra shot", flags);
        var oat = Money(s.OatMilk, "oat milk", flags);
        var unit = quantity > 1 ? Math.Round(price / quantity, 2) : price;
        if (quantity > 1) flags.Add($"quantity {quantity} read from the text; unit price {unit}");

        var status = Status(s.Status, flags);
        var channel = Channel(s.Channel, flags);
        var (discount, value) = Discount(s.Notes, channel, flags);
        var paidOn = PaidDate(s.DatePaid, s.Date, status, flags);

        var gross = unit * quantity + shot + oat;
        if (discount == "None" && total < gross)
            flags.Add($"total {total} is below price + add-ons {gross} with no discount in the notes");
        if (total > gross && gross > 0)
            flags.Add($"total {total} is above price + add-ons {gross}");
        if (Regex.IsMatch(s.Notes, @"\bpartial\b", RegexOptions.IgnoreCase))
            flags.Add("notes mention a partial payment, which is not imported");
        if (Regex.IsMatch(s.Notes, @"free extra shot", RegexOptions.IgnoreCase))
            flags.Add("free extra shot: the shot is not charged");

        var skip =
            drink.Length == 0 ? "no drink: an add-on or supply charge on its own (oat milk, cup and straw, milk)"
            : total == 0 && discount != "Free" ? "₱0 with no free-drink marker (owner or complimentary drink)"
            : name.Length == 0 ? "no customer name"
            : "";

        return new ParsedRow
        {
            Tab = s.Tab, Row = s.Row, Date = s.Date, RawName = s.Name, Name = name, Payer = payer,
            RawDrink = s.Drink, Drink = drink, Quantity = quantity, UnitPrice = unit,
            ExtraShot = shot, OatMilk = oat, Total = total, Status = status,
            RawChannel = s.Channel, Channel = channel, Discount = discount, DiscountValue = value,
            PaidOn = paidOn, RawPaid = s.DatePaid switch { null => "", DateTime d => d.ToString("yyyy-MM-dd"), var o => Convert.ToString(o, CultureInfo.InvariantCulture) ?? "" },
            Notes = s.Notes, Flags = flags, Skip = skip,
        };
    }

    /// <summary>
    /// "Prince c/o Kee" in the name: Prince drank, Kee paid. Notes say it too:
    /// "c/o kee payment", "payment c/o kee", "sheki c/o payment", "c/o office".
    /// "(Extra c/o Kesha)" in brackets is left in the name for the owner to map.
    /// </summary>
    public static (string Name, string Payer) SplitCareOf(string rawName, string notes, List<string> flags)
    {
        var name = rawName;
        var payer = "";
        var inName = CareOfInName().Match(rawName);
        if (inName.Success)
        {
            name = inName.Groups[1].Value.Trim();
            payer = inName.Groups[2].Value.Trim();
        }

        var inNotes = CareOfInNotes().Match(notes);
        var notesPayer = inNotes.Success ? (inNotes.Groups[1].Success ? inNotes.Groups[1].Value : inNotes.Groups[2].Value) : "";
        if (notesPayer.Length > 0)
        {
            if (payer.Length == 0) payer = notesPayer;
            else if (!payer.Equals(notesPayer, StringComparison.OrdinalIgnoreCase))
                flags.Add($"payer: the name says {payer}, the notes say {notesPayer}; used {payer}");
        }

        return (name, payer);
    }

    /// <summary>"Her-She Latte x4", "Zainab x4" (on the name), "13 drinks worth 89 pesos each".</summary>
    public static (string Drink, int Quantity) SplitQuantity(string rawDrink, string rawName)
    {
        var x = TimesN().Match(rawDrink);
        if (x.Success) return (TimesN().Replace(rawDrink, "").Trim(), int.Parse(x.Groups[1].Value));
        var onName = TimesN().Match(rawName);
        if (onName.Success) return (rawDrink, int.Parse(onName.Groups[1].Value));
        var drinks = NDrinks().Match(rawDrink);
        if (drinks.Success) return (rawDrink, int.Parse(drinks.Groups[1].Value));
        return (rawDrink, 1);
    }

    public static decimal Money(object? value, string column, List<string> flags)
    {
        switch (value)
        {
            case null: return 0;
            case double d: return (decimal)Math.Round(d, 2);
            case string text:
                var cleaned = text.Replace("₱", "").Replace(",", "").Trim();
                if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)) return parsed;
                flags.Add($"{column} \"{text}\" is not a number; read as 0");
                return 0;
            default:
                flags.Add($"{column} \"{value}\" is not a number; read as 0");
                return 0;
        }
    }

    public static string Status(string raw, List<string> flags)
    {
        var lower = raw.Trim().ToLowerInvariant();
        switch (lower)
        {
            case "paid": return "Paid";
            case "unpaid": return "Unpaid";
            case "awaiting": return "Awaiting";
            case "":
                flags.Add("no status: May 2025's tab has no status column");
                return "";
            default:
                flags.Add($"unknown status \"{raw}\"");
                return raw;
        }
    }

    public static string Channel(string raw, List<string> flags)
    {
        var key = raw.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "");
        switch (key)
        {
            case "cash": return "Cash";
            case "gcash": return "GCash";
            case "businessgcash": return "BusinessGCash";
            case "cimb": return "CIMB";
            case "bpi": return "BPI";
            case "ar": return "AR";
            case "freedrink": return "Free";
            case "": return "";
            default:
                flags.Add($"channel \"{raw}\" is not an API channel: Other");
                return "Other";
        }
    }

    /// <summary>
    /// "50% loyalty card", "50% off", "10% hangyo bff perk" → Percent; "less 30" →
    /// Fixed; "FREE DRINK", "100% loyalty card", "100% off" or the channel
    /// "Free Drink" → Free. "Free extra shot" is about the add-on, not the drink.
    /// </summary>
    public static (string Kind, decimal Value) Discount(string notes, string channel, List<string> flags)
    {
        var percent = PercentOff().Match(notes);
        var less = LessN().Match(notes);
        var freeDrink = channel == "Free" || FreeDrink().IsMatch(notes) || (percent.Success && percent.Groups[1].Value == "100");
        if (freeDrink) return ("Free", 0);
        if (percent.Success && less.Success) flags.Add("notes give both a percent and a \"less\" discount; used the percent");
        if (percent.Success) return ("Percent", decimal.Parse(percent.Groups[1].Value, CultureInfo.InvariantCulture));
        if (less.Success) return ("Fixed", decimal.Parse(less.Groups[1].Value, CultureInfo.InvariantCulture));
        return ("None", 0);
    }

    /// <summary>
    /// Dates, "same day", or "Sept 21" / "October 10 paid" (year taken from the
    /// order, moved to the next year when the month falls before it). Anything
    /// else is flagged, and the order date stands in for paid rows.
    /// </summary>
    public static DateOnly? PaidDate(object? raw, DateOnly ordered, string status, List<string> flags)
    {
        switch (raw)
        {
            case null: return status is "Paid" or "" ? ordered : null;
            case DateTime d: return DateOnly.FromDateTime(d);
            case string text when text.Trim().Equals("same day", StringComparison.OrdinalIgnoreCase): return ordered;
            case string text when MonthDay().Match(text) is { Success: true } m:
                var month = Months.FindIndex(name => m.Groups[1].Value.ToLowerInvariant().StartsWith(name)) + 1;
                var day = int.Parse(m.Groups[2].Value);
                var year = ordered.Year + (month < ordered.Month ? 1 : 0);
                if (month > 0 && day <= DateTime.DaysInMonth(year, month)) return new DateOnly(year, month, day);
                break;
        }

        var shown = raw is double n ? n.ToString(CultureInfo.InvariantCulture) : raw.ToString();
        flags.Add($"date paid \"{shown}\" is not a date; used the order date");
        return status is "Paid" or "" ? ordered : null;
    }

    private static readonly List<string> Months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    [GeneratedRegex(@"^([^()]+?)\s+c/o\s+([^()]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex CareOfInName();

    [GeneratedRegex(@"c/o\s+(?!payment)([a-z]+)|([a-z]+)\s+c/o\s+payment", RegexOptions.IgnoreCase)]
    private static partial Regex CareOfInNotes();

    [GeneratedRegex(@"\s*\bx\s*(\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TimesN();

    [GeneratedRegex(@"^(\d+)\s+drinks?\b", RegexOptions.IgnoreCase)]
    private static partial Regex NDrinks();

    [GeneratedRegex(@"(\d+)\s*%")]
    private static partial Regex PercentOff();

    [GeneratedRegex(@"\bless\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex LessN();

    [GeneratedRegex(@"free\s+drink", RegexOptions.IgnoreCase)]
    private static partial Regex FreeDrink();

    [GeneratedRegex(@"^\s*([a-z]+)\.?\s+(\d{1,2})\b", RegexOptions.IgnoreCase)]
    private static partial Regex MonthDay();
}
