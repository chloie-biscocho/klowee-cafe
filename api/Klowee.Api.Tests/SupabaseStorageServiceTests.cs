using System.Net;
using System.Text;
using Klowee.Api.Common;
using Klowee.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Klowee.Api.Tests;

/// <summary>
/// The transport's own rules, against a stubbed HTTP handler instead of the
/// network. The upload tests use <see cref="FakeStorageService"/> and never
/// reach this class, so its handling of Supabase's answers is tested here.
/// </summary>
public class SupabaseStorageServiceTests
{
    [Fact]
    public async Task Delete_WhenSupabaseAnswers404_IsASuccess()
    {
        var service = ServiceAnswering(HttpStatusCode.NotFound, "");

        await service.DeleteAsync("events/2026/10/gone.png", CancellationToken.None);
    }

    [Fact]
    public async Task Delete_WhenSupabaseAnswers400NoSuchKey_IsASuccess()
    {
        // The shape Supabase returned during handover 05's live smoke.
        var service = ServiceAnswering(
            HttpStatusCode.BadRequest,
            """{"statusCode":"404","error":"not_found","message":"Object not found","code":"NoSuchKey"}""");

        await service.DeleteAsync("events/2026/10/gone.png", CancellationToken.None);
    }

    [Theory]
    [InlineData("""{"statusCode":"400","error":"InvalidKey","message":"Invalid key"}""")]
    [InlineData("not json")]
    public async Task Delete_WhenSupabaseAnswersAnyOther400_Throws(string body)
    {
        var service = ServiceAnswering(HttpStatusCode.BadRequest, body);

        await Assert.ThrowsAsync<StorageException>(
            () => service.DeleteAsync("events/2026/10/x.png", CancellationToken.None));
    }

    [Fact]
    public async Task Delete_WhenSupabaseSucceeds_SendsTheBucketPath()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        var service = Create(handler);

        await service.DeleteAsync("events/2026/10/x.png", CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.Request!.Method);
        Assert.Equal(
            "https://abcd.supabase.co/storage/v1/object/media/events/2026/10/x.png",
            handler.Request.RequestUri!.ToString());
    }

    private static SupabaseStorageService ServiceAnswering(HttpStatusCode status, string body) =>
        Create(new StubHandler(status, body));

    private static SupabaseStorageService Create(HttpMessageHandler handler)
    {
        var options = new SupabaseOptions { Url = "https://abcd.supabase.co", ServiceRoleKey = "test-key" };
        var client = new HttpClient(handler) { BaseAddress = new Uri(options.ProjectUrl + "/") };
        return new SupabaseStorageService(
            new SingleClientFactory(client),
            Options.Create(options),
            NullLogger<SupabaseStorageService>.Instance);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
