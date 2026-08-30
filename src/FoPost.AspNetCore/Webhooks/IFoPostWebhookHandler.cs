namespace FoPost.AspNetCore;

/// <summary>
/// Handles a verified FoPost webhook. Register implementations with
/// <c>services.AddFoPostWebhookHandler&lt;T&gt;()</c>; every registered handler that
/// subscribes to the event is invoked in registration order.
/// </summary>
public interface IFoPostWebhookHandler
{
    /// <summary>
    /// Events this handler wants. An empty collection — the default — means every event.
    /// </summary>
    IReadOnlyCollection<string> Events => [];

    /// <summary>
    /// Runs the handler. Throwing surfaces as a 500, which FoPost retries with backoff, so
    /// throw when the work should be retried and swallow when it should not.
    /// </summary>
    Task HandleAsync(FoPostWebhookEvent webhookEvent, CancellationToken cancellationToken);
}
