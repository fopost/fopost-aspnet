namespace FoPost.AspNetCore;

/// <summary>Names and paths this integration uses, exposed so callers can reuse them.</summary>
public static class FoPostDefaults
{
    /// <summary>
    /// The named <see cref="System.Net.Http.IHttpClientFactory"/> client the FoPost client is
    /// built on. Reach for it to add your own delegating handlers or Polly policies.
    /// </summary>
    public const string HttpClientName = "FoPost";

    /// <summary>Default route <c>MapFoPostWebhook</c> listens on.</summary>
    public const string WebhookPath = "/fopost/webhook";

    /// <summary>Default name of the health check registered by <c>AddFoPostHealthCheck</c>.</summary>
    public const string HealthCheckName = "fopost";

    /// <summary>Hex HMAC-SHA256 of the raw body, prefixed <c>sha256=</c>.</summary>
    public const string SignatureHeader = "X-FoPost-Signature";

    /// <summary>The event name, mirroring the body's <c>event</c> field.</summary>
    public const string EventHeader = "X-FoPost-Event";

    /// <summary>Delivery attempt id, unique per send.</summary>
    public const string DeliveryHeader = "X-FoPost-Delivery";
}
