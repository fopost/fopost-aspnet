using FoPost.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Maps the endpoint a FoPost blog website target delivers articles to.</summary>
public static class FoPostArticleEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps a <c>POST</c> endpoint that verifies each delivery against
    /// <c>FoPost:Blog:WebhookSecret</c>, answers <c>article.test</c> with <c>{ "ok": true }</c>
    /// and hands <c>article.published</c> to the registered
    /// <see cref="IFoPostArticlePublishedHandler"/>. A bad signature is a <c>401</c>.
    /// </summary>
    public static RouteHandlerBuilder MapFoPostArticles(
        this IEndpointRouteBuilder endpoints,
        string pattern = FoPostDefaults.ArticlesPath)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Cast to Delegate so the IResult is written, not discarded (see MapFoPostWebhook).
        return endpoints
            .MapPost(pattern, (Delegate)FoPostArticleEndpoint.HandleAsync)
            .AllowAnonymous();
    }
}
