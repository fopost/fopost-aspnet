using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FoPost.AspNetCore.Tests;

public class ArticleEndpointTests
{
    private const string Secret = "whsec_site";

    private const string TestBody = """
        {"event":"article.test","data":{},"timestamp":"2026-10-11T10:00:00.000Z"}
        """;

    private const string PublishedBody = """
        {"event":"article.published","data":{"id":"art_1","title":"Hello","slug":"hello","excerpt":null,"metaTitle":null,"metaDescription":null,"bodyMarkdown":"# Hello","bodyHtml":"<h1>Hello</h1>","featuredImage":null,"images":[],"tags":["news"],"keyword":"hello","wordCount":1,"publishedAt":"2026-10-11T10:00:00.000Z","publishedUrl":null},"timestamp":"2026-10-11T10:00:00.000Z"}
        """;

    [Fact]
    public async Task A_signed_test_delivery_is_acknowledged()
    {
        using var host = await StartAsync(_ => Task.FromResult<string?>(null));
        using var client = host.GetTestClient();

        var response = await client.SendAsync(Request(TestBody, FoPostWebhookEvents.ArticleTest));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task A_tampered_delivery_is_rejected_and_no_handler_runs()
    {
        var called = false;
        using var host = await StartAsync(_ =>
        {
            called = true;
            return Task.FromResult<string?>(null);
        });
        using var client = host.GetTestClient();

        var request = Request(PublishedBody, FoPostWebhookEvents.ArticlePublished);
        request.Content = new StringContent(PublishedBody.Replace("Hello", "Hacked"), Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(called);
    }

    [Fact]
    public async Task A_published_article_reaches_the_handler_and_its_url_is_echoed()
    {
        BlogArticlePublishedPayload? received = null;
        using var host = await StartAsync(article =>
        {
            received = article;
            return Task.FromResult<string?>("https://yourbrand.com/blog/" + article.Slug);
        });
        using var client = host.GetTestClient();

        var response = await client.SendAsync(Request(PublishedBody, FoPostWebhookEvents.ArticlePublished));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(received);
        Assert.Equal("art_1", received!.Id);
        Assert.Equal("Hello", received.Title);
        Assert.Equal("<h1>Hello</h1>", received.BodyHtml);
        Assert.Equal(["news"], received.Tags);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("https://yourbrand.com/blog/hello", json.RootElement.GetProperty("url").GetString());
    }

    private static HttpRequestMessage Request(string body, string eventName)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, FoPostDefaults.ArticlesPath)
        {
            Content = new StringContent(body, Encoding.UTF8),
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation(FoPostDefaults.EventHeader, eventName);
        request.Headers.TryAddWithoutValidation(FoPostDefaults.DeliveryHeader, "delivery-1");
        request.Headers.TryAddWithoutValidation(FoPostDefaults.TimestampHeader, timestamp);
        request.Headers.TryAddWithoutValidation(
            FoPostDefaults.SignatureHeader,
            FoPostWebhookSignature.Compute(Encoding.UTF8.GetBytes(body), Secret));
        request.Headers.TryAddWithoutValidation(
            FoPostDefaults.TimestampedSignatureHeader,
            FoPostWebhookSignature.Compute(Encoding.UTF8.GetBytes(timestamp + "." + body), Secret));

        return request;
    }

    private static Task<IHost> StartAsync(Func<BlogArticlePublishedPayload, Task<string?>> onPublished) =>
        new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddFoPost(options =>
                    {
                        options.ApiKey = "fp_test";
                        options.Blog.WebhookSecret = Secret;
                    });
                    services.AddFoPostArticleHandler((article, _) => onPublished(article));
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapFoPostArticles());
                }))
            .StartAsync();
}
