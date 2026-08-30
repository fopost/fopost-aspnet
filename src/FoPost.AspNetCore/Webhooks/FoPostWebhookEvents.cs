namespace FoPost.AspNetCore;

/// <summary>Event names FoPost sends to a webhook, as configured in the dashboard.</summary>
public static class FoPostWebhookEvents
{
    /// <summary>Every account on a post published successfully.</summary>
    public const string PostPublished = "post.published";

    /// <summary>No account on the post published.</summary>
    public const string PostFailed = "post.failed";

    /// <summary>Some accounts published, some did not.</summary>
    public const string PostPartiallyFailed = "post.partially_failed";

    /// <summary>One account's delivery went live.</summary>
    public const string DeliveryPublished = "delivery.published";

    /// <summary>One account's delivery failed for good.</summary>
    public const string DeliveryFailed = "delivery.failed";

    /// <summary>One account's delivery was pushed back and will be retried.</summary>
    public const string DeliveryDelayed = "delivery.delayed";

    /// <summary>A connected account's token or health state changed.</summary>
    public const string AccountHealthChanged = "account.health_changed";
}
