using System.Globalization;
using System.Text;
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

        var body = await ReadBodyAsync(context).ConfigureAwait(false);

        context.Request.Headers.TryGetValue(FoPostDefaults.SignatureHeader, out var signature);
        if (!FoPostWebhookSignature.Verify(body.Span, signature.ToString(), options.WebhookSecret))
        {
            logger.LogWarning("Rejected a FoPost webhook whose signature did not match.");

            return Results.Unauthorized();
        }

        JsonNode? payload;
        try
        {
            payload = JsonNode.Parse(Encoding.UTF8.GetString(body.Span));
        }
        catch (JsonException)
        {
            logger.LogWarning("Rejected a signed FoPost webhook whose body was not JSON.");

            return Results.BadRequest();
        }

        context.Request.Headers.TryGetValue(FoPostDefaults.EventHeader, out var eventHeader);
        context.Request.Headers.TryGetValue(FoPostDefaults.DeliveryHeader, out var deliveryHeader);

        var name = ReadString(payload, "event") ?? eventHeader.ToString();
        if (string.IsNullOrEmpty(name))
        {
            logger.LogWarning("Rejected a signed FoPost webhook that named no event.");

            return Results.BadRequest();
        }

        var webhookEvent = new FoPostWebhookEvent
        {
            Event = name,
            DeliveryId = NullIfEmpty(deliveryHeader.ToString()),
            Timestamp = ReadTimestamp(payload),
            Data = (payload as JsonObject)?["data"],
            Payload = payload,
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

    private static async Task<ReadOnlyMemory<byte>> ReadBodyAsync(HttpContext context)
    {
        // The signature covers the bytes on the wire, so nothing may re-encode them first.
        using var buffer = new MemoryStream();

        await context.Request.Body.CopyToAsync(buffer, context.RequestAborted).ConfigureAwait(false);

        return buffer.ToArray();
    }

    private static string? ReadString(JsonNode? payload, string field)
    {
        if (payload is not JsonObject obj || !obj.TryGetPropertyValue(field, out var node))
        {
            return null;
        }

        return node is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : null;
    }

    private static DateTimeOffset? ReadTimestamp(JsonNode? payload) =>
        DateTimeOffset.TryParse(
            ReadString(payload, "timestamp"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
