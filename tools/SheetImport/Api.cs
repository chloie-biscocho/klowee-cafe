using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Klowee.SheetImport;

public record Item(Guid Id, string Name, bool IsArchived);

/// <summary>The Klowee admin API, signed in as an owner. Only what the import reads so far.</summary>
public sealed class Api : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;

    private Api(HttpClient http) => _http = http;

    public static async Task<Api> SignInAsync(string baseUrl, string email, string password)
    {
        var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/api/"), Timeout = TimeSpan.FromSeconds(100) };
        var response = await http.PostAsJsonAsync("auth/login", new { email, password });
        if (!response.IsSuccessStatusCode)
        {
            http.Dispose();
            throw new ImportException($"Sign-in failed: HTTP {(int)response.StatusCode}. Check KLOWEE_OWNER_EMAIL and KLOWEE_OWNER_PASSWORD.");
        }

        var token = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("accessToken").GetString();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Api(http);
    }

    public async Task<List<Item>> ItemsAsync()
    {
        using var response = await _http.GetAsync("menu/items?includeArchived=true");
        if (!response.IsSuccessStatusCode)
        {
            throw new ImportException($"GET /api/menu/items failed: HTTP {(int)response.StatusCode}");
        }

        return (await response.Content.ReadFromJsonAsync<List<Item>>(Json))!;
    }

    public void Dispose() => _http.Dispose();
}
