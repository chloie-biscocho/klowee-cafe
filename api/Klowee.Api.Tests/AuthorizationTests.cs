using System.Net;

namespace Klowee.Api.Tests;

public class AuthorizationTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public AuthorizationTests(KloweeApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/menu/categories")]
    [InlineData("/api/menu/items")]
    [InlineData("/api/menu/add-ons")]
    [InlineData("/api/menu/versions")]
    public async Task MenuEndpoints_WithoutToken_Return401(string path)
    {
        var client = await _factory.CreateAnonymousClientAsync();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MenuEndpoints_WithToken_AreReachable()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.GetAsync("/api/menu/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>The two endpoints that opt out of the fallback policy.</summary>
    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/menu/versions/current?context=PopUp")]
    public async Task PublicEndpoints_WithoutToken_AreNotRejected(string path)
    {
        var client = await _factory.CreateAnonymousClientAsync();

        var response = await client.GetAsync(path);

        // 404 is a valid answer for "current" when nothing is published; 401 is not.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
