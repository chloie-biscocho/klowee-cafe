using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Klowee.SheetImport;

// The admin API's shapes, as much of them as the import reads.
public record Category(Guid Id, string Name, int SortOrder, Guid? LoyaltyClubId = null);
public record Item(Guid Id, string Name, Guid CategoryId, bool IsArchived, string LoyaltyMode = "Inherit", Guid? LoyaltyClubId = null);
public record VersionSummary(Guid Id, string Name, string Context, DateOnly EffectiveFrom, bool IsPublished, int ItemCount);
public record VersionItem(Guid Id, Guid MenuItemId, string Name, decimal Price);
public record VersionDetail(Guid Id, string Name, string Context, DateOnly EffectiveFrom, bool IsPublished, List<VersionItem> Items);
public record PricedLine(Guid MenuItemId, decimal Price, bool IsAvailable, bool IsFeatured, int SortOrder);
public record AddOn(Guid Id, string Name, decimal Price, bool IsActive);
public record Customer(Guid Id, string Name, string? Nickname, bool IsActive, decimal OutstandingBalance, List<ClubCard> Clubs);
public record ClubCard(Guid ClubId, string Name, string Style, int SlotCount, int Position, int Cycle, List<ClubReward> Rewards);
public record ClubReward(Guid RewardId, int Slot, string Label, string Type, string State, int Cycle);
public record PersonRef(Guid Id, string Name);
public record OrderSummary(Guid Id, DateOnly OrderedOn, PersonRef Customer, decimal Total, decimal Paid, string Status);
public record OrderDetail(Guid Id, DateOnly OrderedOn, string? Notes, string DiscountType, decimal Subtotal, decimal DiscountAmount, decimal Total, decimal Paid, string Status);
public record Paged<T>(List<T> Items, int Page, int PageSize, int TotalCount);
// An order in full, for the cardholder scan (handover 16a).
public record FullAddOn(Guid AddOnId, string Name, decimal UnitPrice);
public record FullReward(string Type);
public record FullItem(Guid MenuItemId, string Name, int Quantity, List<FullAddOn> AddOns, FullReward? Reward);
public record FullOrder(Guid Id, DateOnly OrderedOn, PersonRef Customer, string? Notes, string DiscountType, decimal DiscountValue, string Status, List<FullItem> Items);
public record SummaryReport(decimal Revenue, decimal Collected, int OrderCount);
public record Setting(string Key, string Value);

public record OrderLine(Guid MenuVersionItemId, int Quantity, List<Guid> AddOnIds, Guid? RewardId = null);
public record NewOrder(Guid CustomerId, Guid? PayerId, DateOnly OrderedOn, string Notes, string DiscountType, decimal DiscountValue, List<OrderLine> Items);
public record NewPayment(decimal Amount, string Channel, DateOnly PaidOn, string? Notes);

/// <summary>
/// The Klowee admin API over HTTP, signed in as an owner. Every write goes
/// through the endpoints the admin app uses, so every rule applies.
/// </summary>
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

    public Task<List<Category>> CategoriesAsync() => GetAsync<List<Category>>("menu/categories");
    public Task<List<Item>> ItemsAsync() => GetAsync<List<Item>>("menu/items?includeArchived=true");
    public Task<List<VersionSummary>> VersionsAsync() => GetAsync<List<VersionSummary>>("menu/versions");
    public Task<VersionDetail> VersionAsync(Guid id) => GetAsync<VersionDetail>($"menu/versions/{id}");
    public Task<List<AddOn>> AddOnsAsync() => GetAsync<List<AddOn>>("menu/add-ons");
    public Task<List<Setting>> SettingsAsync() => GetAsync<List<Setting>>("settings");
    public Task<List<Customer>> CustomersAsync() => GetAsync<List<Customer>>("customers?includeInactive=true");
    public Task<Customer> CustomerAsync(Guid id) => GetAsync<Customer>($"customers/{id}");
    public Task<OrderDetail> OrderAsync(Guid id) => GetAsync<OrderDetail>($"orders/{id}");
    public Task<FullOrder> FullOrderAsync(Guid id) => GetAsync<FullOrder>($"orders/{id}");

    public Task<Paged<OrderSummary>> OrdersAsync(DateOnly from, DateOnly to, int page) =>
        GetAsync<Paged<OrderSummary>>($"orders?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&page={page}&pageSize=200");

    public Task<SummaryReport> SummaryAsync(DateOnly from, DateOnly to) =>
        GetAsync<SummaryReport>($"reports/summary?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

    public Task<Category> CreateCategoryAsync(string name, int sortOrder) =>
        SendAsync<Category>(HttpMethod.Post, "menu/categories", new { name, sortOrder });

    public Task<Item> CreateItemAsync(string name, string description, Guid categoryId) =>
        SendAsync<Item>(HttpMethod.Post, "menu/items", new { name, description, categoryId });

    public Task<Item> SetArchivedAsync(Guid id, bool archived) =>
        SendAsync<Item>(HttpMethod.Patch, $"menu/items/{id}/{(archived ? "archive" : "unarchive")}", null);

    public Task<VersionDetail> CreateVersionAsync(string name, string context, DateOnly effectiveFrom) =>
        SendAsync<VersionDetail>(HttpMethod.Post, "menu/versions", new { name, context, effectiveFrom });

    public Task<VersionDetail> ReplaceItemsAsync(Guid id, IReadOnlyList<PricedLine> lines) =>
        SendAsync<VersionDetail>(HttpMethod.Put, $"menu/versions/{id}/items", lines);

    public Task<Customer> CreateCustomerAsync(string name) =>
        SendAsync<Customer>(HttpMethod.Post, "customers", new { name });

    public Task<OrderDetail> CreateOrderAsync(NewOrder order) => SendAsync<OrderDetail>(HttpMethod.Post, "orders", order);

    /// <summary>Like <see cref="CreateOrderAsync"/>, but a 409 (a reward the API won't give) returns null instead of throwing.</summary>
    public async Task<OrderDetail?> TryCreateOrderAsync(NewOrder order)
    {
        using var response = await _http.PostAsJsonAsync("orders", order, Json);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return null;
        }

        await EnsureAsync(response, "POST", "orders");
        return (await response.Content.ReadFromJsonAsync<OrderDetail>(Json))!;
    }

    public Task<OrderDetail> AddPaymentAsync(Guid orderId, NewPayment payment) =>
        SendAsync<OrderDetail>(HttpMethod.Post, $"orders/{orderId}/payments", payment);

    public async Task DeleteOrderAsync(Guid id)
    {
        using var response = await _http.DeleteAsync($"orders/{id}");
        await EnsureAsync(response, "DELETE", $"orders/{id}");
    }

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await _http.GetAsync(path);
        await EnsureAsync(response, "GET", path);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        using var response = await _http.SendAsync(request);
        await EnsureAsync(response, method.Method, path);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task EnsureAsync(HttpResponseMessage response, string method, string path)
    {
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadAsStringAsync();
            throw new ImportException($"{method} /api/{path} failed: HTTP {(int)response.StatusCode} {problem}");
        }
    }

    public void Dispose() => _http.Dispose();
}
