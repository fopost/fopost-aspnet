using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FoPost.AspNetCore.Tests;

public class WebhookEndpointTests
{
    private const string Secret = "whsec_test";

    private const string Body = """
        {"event":"post.published","data":{"post_id":"9b2f6c1e"},"timestamp":"2026-08-30T10:00:00.000Z"}
        """;

    [Fact]
    public async Task A_valid_signature_is_accepted_and_reaches_the_handler()
    {
        var recorder = new RecordingWebhookHandler();
        using var host = await StartAsync(recorder);
        using var client = host.GetTestClient();

        var response = await client.SendAsync(Request(Body, Signature(Body)));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var received = Assert.Single(recorder.Received);
        Assert.Equal(FoPostWebhookEvents.PostPublished, received.Event);
        Assert.Equal("delivery-1", received.DeliveryId);
        Assert.Equal("9b2f6c1e", received.Data?["post_id"]?.GetValue<string>());
        Assert.Equal(
            new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero),
            received.Timestamp);
    }

    [Fact]
    public async Task A_bad_signature_is_rejected_and_no_handler_runs()
    {
        var recorder = new RecordingWebhookHandler();
        using var host = await StartAsync(recorder);
        using var client = host.GetTestClient();

        var response = await client.SendAsync(Request(Body, Signature("{\"event\":\"other\"}")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(recorder.Received);
    }

    [Fact]
    public async Task A_missing_signature_header_is_rejected()
    {
        var recorder = new RecordingWebhookHandler();
        using var host = await StartAsync(recorder);
        using var client = host.GetTestClient();

        var response = await client.SendAsync(Request(Body, signature: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(recorder.Received);
    }

    [Fact]
    public async Task A_signature_over_a_different_secret_is_rejected()
    {
        var recorder = new RecordingWebhookHandler();
        using var host = await StartAsync(recorder);
        using var client = host.GetTestClient();

        var forged = FoPostWebhookSignature.Compute(Encoding.UTF8.GetBytes(Body), "whsec_wrong");
        var response = await client.SendAsync(Request(Body, forged));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(recorder.Received);
    }

    [Fact]
    public async Task A_handler_that_did_not_subscribe_is_skipped()
    {
        var recorder = new RecordingWebhookHandler(FoPostWebhookEvents.DeliveryFailed);
        using var host = await StartAsync(recorder);
        using var client = host.GetTestClient();

        var response = await client.SendAsync(Request(Body, Signature(Body)));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(recorder.Received);
    }

    [Fact]
    public async Task A_signed_body_that_is_not_json_is_a_bad_request()
    {
        var recorder = new RecordingWebhookHandler();
        using var host = await StartAsync(recorder);
        using var client = host.GetTestClient();

        const string garbage = "not json";
        var response = await client.SendAsync(Request(garbage, Signature(garbage)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(recorder.Received);
    }

    [Fact]
    public async Task Without_a_configured_secret_nothing_is_accepted()
    {
        var recorder = new RecordingWebhookHandler();
        using var host = await StartAsync(recorder, secret: null);
        using var client = host.GetTestClient();

        var response = await client.SendAsync(Request(Body, Signature(Body)));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(recorder.Received);
    }

    private static string Signature(string body) =>
        FoPostWebhookSignature.Compute(Encoding.UTF8.GetBytes(body), Secret);

    private static HttpRequestMessage Request(string body, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, FoPostDefaults.WebhookPath)
        {
            Content = new StringContent(body, Encoding.UTF8),
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation(FoPostDefaults.EventHeader, FoPostWebhookEvents.PostPublished);
        request.Headers.TryAddWithoutValidation(FoPostDefaults.DeliveryHeader, "delivery-1");
        if (signature is not null)
        {
            request.Headers.TryAddWithoutValidation(FoPostDefaults.SignatureHeader, signature);
        }

        return request;
    }

    private static Task<IHost> StartAsync(RecordingWebhookHandler recorder, string? secret = Secret) =>
        new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddFoPost(options =>
                    {
                        options.ApiKey = "fp_test";
                        options.WebhookSecret = secret;
                    });
                    services.AddSingleton<IFoPostWebhookHandler>(recorder);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapFoPostWebhook());
                }))
            .StartAsync();
}
