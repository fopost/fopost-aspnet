using System.Text.Json.Nodes;

namespace FoPost.AspNetCore;

/// <summary>
/// One verified webhook delivery. The body FoPost posts is
/// <c>{ "event": "…", "data": { … }, "timestamp": "…" }</c>.
/// </summary>
public sealed class FoPostWebhookEvent
{
    /// <summary>Event name, e.g. <c>post.published</c>. See <see cref="FoPostWebhookEvents"/>.</summary>
    public required string Event { get; init; }

    /// <summary>
    /// Delivery attempt id from <c>X-FoPost-Delivery</c>. Unique per send, so it is what to
    /// key on when making a handler idempotent.
    /// </summary>
    public string? DeliveryId { get; init; }

    /// <summary>When the API raised the event, from the body's <c>timestamp</c>.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>The body's <c>data</c> object — the payload for this event.</summary>
    public JsonNode? Data { get; init; }

    /// <summary>The whole decoded body, envelope included.</summary>
    public JsonNode? Payload { get; init; }

    /// <summary>The exact bytes that were signed, for re-verifying or archiving.</summary>
    public required ReadOnlyMemory<byte> RawBody { get; init; }
}
