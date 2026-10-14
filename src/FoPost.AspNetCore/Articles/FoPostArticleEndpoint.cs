using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FoPost.AspNetCore;

internal static class FoPostArticleEndpoint
{
    private const string LoggerName = "FoPost.AspNetCore.Articles";

    public static async Task<IResult> HandleAsync(HttpContext context)
    {
        var services = context.RequestServices;
        var secret = services.GetRequiredService<IOptions<FoPostOptions>>().Value.Blog.WebhookSecret;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerName);

        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogError(
                "A FoPost article arrived but {Section}:Blog:{Setting} is not configured; the delivery was refused.",
                FoPostOptions.SectionName,
                nameof(FoPostBlogOptions.WebhookSecret));

            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        var body = await FoPostWebhookRequest.ReadBodyAsync(context).ConfigureAwait(false);

        FoPostWebhookEnvelope envelope;
        try
        {
            envelope = FoPostWebhooks.Verify(body.Span, FoPostWebhookRequest.Headers(context), secret);
        }
        catch (FoPostWebhookVerificationException)
        {
            logger.LogWarning("Rejected a FoPost article delivery that did not verify.");

            return Results.Unauthorized();
        }

        if (!string.Equals(envelope.Event, FoPostWebhookEvents.ArticlePublished, StringComparison.OrdinalIgnoreCase))
        {
            return Ok;
        }

        var handler = services.GetService<IFoPostArticlePublishedHandler>();
        if (handler is null)
        {
            logger.LogError("A FoPost article arrived but no IFoPostArticlePublishedHandler is registered.");

            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        BlogArticlePublishedPayload article;
        try
        {
            article = envelope.DataAs<BlogArticlePublishedPayload>();
        }
        catch (FoPostWebhookVerificationException)
        {
            logger.LogWarning("Rejected a signed FoPost article delivery whose data was not an article.");

            return Results.BadRequest();
        }

        var url = await handler.HandleAsync(article, context.RequestAborted).ConfigureAwait(false);

        return url is null ? Ok : Json(JsonSerializer.Serialize(new { url }));
    }

    private static IResult Ok => Json("""{"ok":true}""");

    private static IResult Json(string body) => Results.Content(body, "application/json");
}
