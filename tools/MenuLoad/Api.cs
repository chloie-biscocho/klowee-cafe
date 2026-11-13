using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Klowee.MenuLoad;

// The admin API's shapes, as much of them as the loader reads.
public record Category(Guid Id, string Name, int SortOrder, string? Description, Guid? LoyaltyClubId);
public record Item(Guid Id, string Name, string? Description, Guid CategoryId, string CategoryName, string? PhotoUrl, bool IsArchived,
    string LoyaltyMode = "Inherit", Guid? LoyaltyClubId = null);
public record VersionSummary(Guid Id, string Name, string Context, DateOnly EffectiveFrom, bool IsPublished, int ItemCount);
public record VersionItem(Guid Id, Guid MenuItemId, string Name, decimal Price, bool IsAvailable, bool IsFeatured, int SortOrder);
public record VersionDetail(Guid Id, string Name, string Context, DateOnly EffectiveFrom, bool IsPublished, List<VersionItem> Items);
public record PricedLine(Guid MenuItemId, decimal Price, bool IsAvailable, bool IsFeatured, int SortOrder);

/// <summary>
/// The Klowee admin API over HTTP, signed in as an owner. Every write goes
/// through the same endpoints the admin app uses, so every rule applies.
/// </summary>
public sealed class Api : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
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
            throw new LoadException($"Sign-in failed: HTTP {(int)response.StatusCode}. Check KLOWEE_OWNER_EMAIL and KLOWEE_OWNER_PASSWORD.");
        }

        var token = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("accessToken").GetString();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new Api(http);
    }

    public Task<List<Category>> CategoriesAsync() => GetAsync<List<Category>>("menu/categories");
    public Task<List<Item>> ItemsAsync() => GetAsync<List<Item>>("menu/items?includeArchived=true");
    public Task<List<VersionSummary>> VersionsAsync() => GetAsync<List<VersionSummary>>("menu/versions");
    public Task<VersionDetail> VersionAsync(Guid id) => GetAsync<VersionDetail>($"menu/versions/{id}");
    public Task<VersionDetail> CurrentAsync(string context) => GetAsync<VersionDetail>($"menu/versions/current?context={context}");

    public Task<Category> CreateCategoryAsync(string name, int sortOrder) => SendAsync<Category>(HttpMethod.Post, "menu/categories", new { name, sortOrder });
    /// <summary>The PUT replaces every field, so the description and loyalty card are sent back as they were.</summary>
    public Task<Category> UpdateCategoryAsync(Category existing, string name, int sortOrder) =>
        SendAsync<Category>(HttpMethod.Put, $"menu/categories/{existing.Id}",
            new { name, sortOrder, description = existing.Description, loyaltyClubId = existing.LoyaltyClubId });

    public Task<Item> CreateItemAsync(string name, string description, Guid categoryId) =>
        SendAsync<Item>(HttpMethod.Post, "menu/items", new { name, description, categoryId });

    /// <summary>The PUT replaces every field, so the photo and loyalty card setting are sent back as they were.</summary>
    public Task<Item> UpdateItemAsync(Item existing, string name, string description, Guid categoryId) =>
        SendAsync<Item>(HttpMethod.Put, $"menu/items/{existing.Id}", new
        {
            name, description, categoryId, photoUrl = existing.PhotoUrl,
            loyaltyMode = existing.LoyaltyMode, loyaltyClubId = existing.LoyaltyClubId
        });

    public Task<Item> SetArchivedAsync(Guid id, bool archived) =>
        SendAsync<Item>(HttpMethod.Patch, $"menu/items/{id}/{(archived ? "archive" : "unarchive")}", null);

    public Task<VersionDetail> CreateVersionAsync(string name, string context, DateOnly effectiveFrom) =>
        SendAsync<VersionDetail>(HttpMethod.Post, "menu/versions", new { name, context, effectiveFrom });

    public Task<VersionDetail> ReplaceItemsAsync(Guid id, IReadOnlyList<PricedLine> lines) =>
        SendAsync<VersionDetail>(HttpMethod.Put, $"menu/versions/{id}/items", lines);

    public Task<VersionDetail> SetPublishedAsync(Guid id, bool published) =>
        SendAsync<VersionDetail>(HttpMethod.Patch, $"menu/versions/{id}/{(published ? "publish" : "unpublish")}", null);

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await _http.GetAsync(path);
        return await ReadAsync<T>(response, "GET", path);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        using var response = await _http.SendAsync(request);
        return await ReadAsync<T>(response, method.Method, path);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, string method, string path)
    {
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadAsStringAsync();
            throw new LoadException($"{method} /api/{path} failed: HTTP {(int)response.StatusCode} {problem}");
        }

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public void Dispose() => _http.Dispose();
}

public sealed class LoadException(string message) : Exception(message);
