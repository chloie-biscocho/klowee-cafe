using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Klowee.SheetImport;

/// <summary>
/// `straystamps [--apply]` (handover 17a): lists the stamps given by mistake
/// to carried-over cards (an order already on the paper card, paid after the
/// carry-over), each card's position before and after, and any reward an order
/// already used that depended on them. Read only unless `--apply`, which asks
/// the API to void them through its normal reconcile (money never moves).
///
///   KLOWEE_API_URL      the API (default http://localhost:5181, dev)
///   KLOWEE_OWNER_EMAIL  an owner's email
///   KLOWEE_OWNER_PASSWORD optional; asked for without echo when unset
///
/// Self-contained (its own sign-in and prompt) so it touches no shared file
/// but one line in Program.cs.
/// </summary>
public static class StrayStampsCommand
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Stamp(string CustomerName, string ClubName, Guid OrderId, DateOnly OrderedOn, int Stamps);
    private sealed record Position(string CustomerName, string ClubName, DateOnly CarryOverDay, string Before, string After);
    private sealed record UsedReward(string CustomerName, string ClubName, string Reward, int CardNumber, int Slot, Guid OrderId, DateOnly OrderedOn);
    private sealed record Listing(List<Stamp> Stamps, List<Position> Positions, List<UsedReward> UsedRewards);

    public static async Task<int> RunAsync(string[] args, TextWriter output)
    {
        var apply = args.Contains("--apply");
        var url = Environment.GetEnvironmentVariable("KLOWEE_API_URL") is { Length: > 0 } u ? u : "http://localhost:5181";
        var email = Environment.GetEnvironmentVariable("KLOWEE_OWNER_EMAIL");
        if (string.IsNullOrWhiteSpace(email))
        {
            Console.Error.WriteLine("Set KLOWEE_OWNER_EMAIL to an owner's email.");
            return 1;
        }

        var password = Environment.GetEnvironmentVariable("KLOWEE_OWNER_PASSWORD") is { Length: > 0 } p ? p : ReadSecret($"Password for {email}: ");

        using var http = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/api/"), Timeout = TimeSpan.FromSeconds(120) };
        var login = await http.PostAsJsonAsync("auth/login", new { email, password });
        if (!login.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Sign-in failed: HTTP {(int)login.StatusCode}.");
            return 1;
        }

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("accessToken").GetString();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        output.WriteLine($"API   {new Uri(url).Host}");
        output.WriteLine($"Mode  {(apply ? "APPLY: void the stamps below" : "list only (nothing is written)")}");
        output.WriteLine();

        var listing = (await http.GetFromJsonAsync<Listing>("loyalty/stray-stamps", Json))!;
        output.Write(Describe(listing));
        if (!apply || listing.Stamps.Count == 0)
        {
            if (listing.Stamps.Count > 0)
            {
                output.WriteLine("Nothing was changed. Read the list, then run again with --apply to void these stamps.");
            }

            return 0;
        }

        var voided = await http.PostAsync("loyalty/stray-stamps/void", null);
        if (!voided.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"Void failed: HTTP {(int)voided.StatusCode}: {await voided.Content.ReadAsStringAsync()}");
            return 1;
        }

        var left = (await voided.Content.ReadFromJsonAsync<Listing>(Json))!;
        output.WriteLine($"Voided {listing.Stamps.Sum(s => s.Stamps)} stamp(s) on {listing.Stamps.Select(s => s.OrderId).Distinct().Count()} order(s). Left: {left.Stamps.Count}.");
        return left.Stamps.Count == 0 ? 0 : 1;
    }

    private static string Describe(Listing listing)
    {
        var s = new StringBuilder();
        if (listing.Stamps.Count == 0)
        {
            s.AppendLine("No stray stamps: no carried-over card has a stamp from an order dated on or before its carry-over day.");
            return s.ToString();
        }

        s.AppendLine($"Stray stamps: {listing.Stamps.Sum(x => x.Stamps)} on {listing.Stamps.Select(x => x.OrderId).Distinct().Count()} order(s).");
        foreach (var stamp in listing.Stamps)
        {
            s.AppendLine($"  {stamp.CustomerName,-16} {stamp.ClubName,-16} order {stamp.OrderedOn:yyyy-MM-dd} ({stamp.OrderId.ToString()[..8]})  {stamp.Stamps} stamp(s)");
        }

        s.AppendLine();
        s.AppendLine("Cards: now -> after voiding");
        foreach (var p in listing.Positions)
        {
            s.AppendLine($"  {p.CustomerName,-16} {p.ClubName,-16} {p.Before} -> {p.After}   (carry-over day {p.CarryOverDay:yyyy-MM-dd})");
        }

        s.AppendLine();
        if (listing.UsedRewards.Count == 0)
        {
            s.AppendLine("Rewards already used that depended on these stamps: none.");
        }
        else
        {
            s.AppendLine("Rewards already used that depended on these stamps (kept; their orders are not undone):");
            foreach (var r in listing.UsedRewards)
            {
                s.AppendLine($"  {r.CustomerName,-16} {r.ClubName,-16} {r.Reward} (card {r.CardNumber}, slot {r.Slot}) on the order of {r.OrderedOn:yyyy-MM-dd} ({r.OrderId.ToString()[..8]})");
            }
        }

        s.AppendLine();
        return s.ToString();
    }

    /// <summary>Reads a line without echoing it; falls back to a plain read when input is redirected.</summary>
    private static string ReadSecret(string prompt)
    {
        Console.Error.Write(prompt);
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var secret = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (secret.Length > 0) secret.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar)) secret.Append(key.KeyChar);
        }

        Console.Error.WriteLine();
        return secret.ToString();
    }
}
