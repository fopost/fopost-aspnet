using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FoPost.AspNetCore;

internal static class FoPostWebhookEndpoint
{
    private const string LoggerName = "FoPost.AspNetCore.Webhooks";

    public static async Task<IResult> HandleAsync(HttpContext context)
    {
        var services = context.RequestServices;
        var options = services.GetRequiredService<IOptions<FoPostOptions>>().Value;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerName);

        if (string.IsNullOrWhiteSpace(options.WebhookSecret))
        {
            // Without a secret nothing can be verified, so accepting the body would be worse
            // than refusing it.
            logger.LogError(
                "A FoPost webhook arrived but {Section}:{Setting} is not configured; the delivery was refused.",
                FoPostOptions.SectionName,
                nameof(FoPostOptions.WebhookSecret));

            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }

        var body = await FoPostWebhookRequest.ReadBodyAsync(context).ConfigureAwait(false);

        FoPostWebhookEnvelope envelope;
        try
        {
            envelope = FoPostWebhooks.Verify(body.Span, FoPostWebhookRequest.Headers(context), options.WebhookSecret);
        }
        catch (FoPostWebhookVerificationException)
        {
            logger.LogWarning("Rejected a FoPost webhook that did not verify.");

            return Results.Unauthorized();
        }

        var webhookEvent = new FoPostWebhookEvent
        {
            Event = envelope.Event,
            DeliveryId = envelope.DeliveryId,
            Timestamp = envelope.Timestamp,
            Data = envelope.Data.ValueKind is JsonValueKind.Undefined ? null : JsonSerializer.SerializeToNode(envelope.Data),
            Payload = JsonNode.Parse(body.Span),
            RawBody = body,
        };

        foreach (var handler in services.GetServices<IFoPostWebhookHandler>())
        {
            var wanted = handler.Events;
            if (wanted.Count > 0 && !wanted.Contains(webhookEvent.Event, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            await handler.HandleAsync(webhookEvent, context.RequestAborted).ConfigureAwait(false);
        }

        return Results.NoContent();
    }
}
