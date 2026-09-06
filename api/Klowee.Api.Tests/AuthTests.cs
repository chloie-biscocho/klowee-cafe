using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Auth;

namespace Klowee.Api.Tests;

public class AuthTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public AuthTests(KloweeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = await _factory.CreateAnonymousClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = KloweeApiFactory.OwnerEmail, Password = KloweeApiFactory.WrongPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // The message must not reveal which half of the credentials was wrong.
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password is", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no such user", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var client = await _factory.CreateAnonymousClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "nobody@klowee.test", Password = KloweeApiFactory.OwnerPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokenThatIdentifiesTheOwner()
    {
        var client = await _factory.CreateAnonymousClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = KloweeApiFactory.OwnerEmail, Password = KloweeApiFactory.OwnerPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(KloweeApiFactory.Json);
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login!.AccessToken));
        Assert.True(login.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(KloweeApiFactory.OwnerEmail, login.User.Email);

        // The same token must now identify the caller on a protected endpoint.
        var authenticated = await _factory.CreateOwnerClientAsync();
        var me = await authenticated.GetFromJsonAsync<UserDto>("/api/auth/me", KloweeApiFactory.Json);

        Assert.NotNull(me);
        Assert.Equal(login.User.Id, me!.Id);
        Assert.Equal(KloweeApiFactory.OwnerEmail, me.Email);
        Assert.Equal(KloweeApiFactory.OwnerDisplayName, me.DisplayName);
    }
}
