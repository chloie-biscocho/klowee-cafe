using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Common;
using Klowee.Api.Contracts.Auth;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace Klowee.Api.Tests;

/// <summary>The production-facing behaviour from handover 08 (decision 013).</summary>
public class HostingTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public HostingTests(KloweeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_AllowsFiveAttemptsPerMinutePerIp_ThenReturns429()
    {
        await _factory.EnsureInitializedAsync();
        using var limited = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<LoginRateLimitOptions>(options => options.PermitLimit = 5)));
        var client = limited.CreateClient();

        var wrong = new LoginRequest { Email = KloweeApiFactory.OwnerEmail, Password = KloweeApiFactory.WrongPassword };

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", wrong);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // The sixth is refused before the password is even checked, right one or not.
        var sixth = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = KloweeApiFactory.OwnerEmail, Password = KloweeApiFactory.OwnerPassword });
        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);

        // Another visitor, as Render's proxy reports it, has their own allowance.
        var elsewhere = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest
            {
                Email = KloweeApiFactory.OwnerEmail,
                Password = KloweeApiFactory.OwnerPassword
            })
        };
        elsewhere.Headers.Add("X-Forwarded-For", "203.0.113.7");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(elsewhere)).StatusCode);

        // Only login is limited.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
    }

    [Fact]
    public async Task Health_IsAnonymous()
    {
        var client = await _factory.CreateAnonymousClientAsync();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
    }

    [Fact]
    public void StartupPlan_ProductionNeverMigratesAndSeedsOnlyTheOwners()
    {
        var plan = StartupPlan.For(Environment("Production"));

        Assert.False(plan.Migrate);
        Assert.True(plan.SeedOwners);
        Assert.False(plan.SeedSampleData);
    }

    [Fact]
    public void StartupPlan_DevelopmentMigratesAndSeedsEverything_TestingNothing()
    {
        Assert.Equal(new StartupPlan(true, true, true), StartupPlan.For(Environment("Development")));
        Assert.Equal(new StartupPlan(false, false, false), StartupPlan.For(Environment("Testing")));
    }

    [Fact]
    public void RequiredConfiguration_InProduction_ListsEveryMissingKeyAsAnEnvironmentVariable()
    {
        var empty = new ConfigurationBuilder().Build();

        var missing = RequiredConfiguration.Missing(empty, Environment("Production"));

        Assert.Equal(
            ["ConnectionStrings__Default", "Jwt__Key", "Supabase__Url", "Supabase__ServiceRoleKey", "App__AllowedOrigins"],
            missing);
    }

    [Fact]
    public void RequiredConfiguration_AllowedOriginsIsOnlyRequiredInProduction()
    {
        var configured = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=db",
                ["Jwt:Key"] = "k",
                ["Supabase:Url"] = "https://x.supabase.co",
                ["Supabase:ServiceRoleKey"] = "k",
                ["App:AllowedOrigins"] = " "
            })
            .Build();

        Assert.Empty(RequiredConfiguration.Missing(configured, Environment("Development")));
        Assert.Equal(["App__AllowedOrigins"], RequiredConfiguration.Missing(configured, Environment("Production")));
    }

    [Fact]
    public void AllowedOrigins_SplitsTrimsAndDropsTrailingSlashes()
    {
        var options = new AppOptions { AllowedOrigins = "https://klowee.vercel.app/, https://klowee-admin.vercel.app,," };

        Assert.Equal(["https://klowee.vercel.app", "https://klowee-admin.vercel.app"], options.ParseAllowedOrigins());
    }

    private static IHostEnvironment Environment(string name) => new HostingEnvironment { EnvironmentName = name };
}
