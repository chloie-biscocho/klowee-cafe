using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Auth;
using Klowee.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Klowee.Api.Tests;

/// <summary>Each test uses its own owner, so changing a password never affects another test.</summary>
public class PasswordTests : IClassFixture<KloweeApiFactory>
{
    private const string Original = "original-password-1";
    private readonly KloweeApiFactory _factory;

    public PasswordTests(KloweeApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, string Email)> OwnerAsync(string name)
    {
        var email = $"{name}@klowee.test";
        await _factory.WithDbAsync(async db =>
        {
            var user = new User { Email = email, DisplayName = name, Role = UserRole.Owner };
            user.PasswordHash = _factory.Services.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, Original);
            db.Users.Add(user);
            return await db.SaveChangesAsync();
        });

        var client = await _factory.CreateAnonymousClientAsync();
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new { email, password = Original }))
            .Content.ReadFromJsonAsync<LoginResponse>(KloweeApiFactory.Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return (client, email);
    }

    private static Task<HttpResponseMessage> ChangeAsync(HttpClient client, string current, string next) =>
        client.PutAsJsonAsync("/api/auth/password", new { currentPassword = current, newPassword = next });

    private async Task<HttpStatusCode> LoginAsync(string email, string password) =>
        (await (await _factory.CreateAnonymousClientAsync()).PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode;

    [Fact]
    public async Task AWrongCurrentPassword_Is401_WithAGenericMessage()
    {
        var (client, _) = await OwnerAsync("wrongcurrent");

        var response = await ChangeAsync(client, "not-the-password", "a-brand-new-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("The current password is incorrect.", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("too-short-1")]          // 11 characters
    [InlineData(Original)]               // unchanged
    public async Task AShortOrUnchangedNewPassword_Is400(string next)
    {
        var (client, email) = await OwnerAsync($"badnew{next.Length}");

        var response = await ChangeAsync(client, Original, next);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await LoginAsync(email, Original));
    }

    [Fact]
    public async Task Success_Is204_TheNewPasswordLogsIn_TheOldDoesNot_AndTheOldTokenStillWorks()
    {
        var (client, email) = await OwnerAsync("changes");

        var response = await ChangeAsync(client, Original, "a-brand-new-password");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await LoginAsync(email, "a-brand-new-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginAsync(email, Original));

        // No token revocation in this phase: a token issued before the change
        // keeps working until it expires (decision 004's lifetime).
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task WithoutAToken_Is401()
    {
        var client = await _factory.CreateAnonymousClientAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await ChangeAsync(client, Original, "a-brand-new-password")).StatusCode);
    }
}
