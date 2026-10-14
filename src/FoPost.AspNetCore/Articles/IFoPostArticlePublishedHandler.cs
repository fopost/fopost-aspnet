
namespace FoPost.AspNetCore;

/// <summary>
/// Receives a published blog article from <c>MapFoPostArticles</c>. Register with
/// <c>services.AddFoPostArticleHandler&lt;T&gt;()</c> or pass a delegate.
/// </summary>
public interface IFoPostArticlePublishedHandler
{
    /// <summary>
    /// Stores the article. Return the URL it now lives at and the receiver echoes it back as
    /// <c>{ "url": "…" }</c>; return <c>null</c> to answer <c>{ "ok": true }</c>. Throwing
    /// surfaces as a 500, which FoPost retries.
    /// </summary>
    Task<string?> HandleAsync(BlogArticlePublishedPayload article, CancellationToken cancellationToken);
}
