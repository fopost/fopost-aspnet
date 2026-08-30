using FoPost.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Maps the endpoint FoPost posts webhook deliveries to.</summary>
public static class FoPostWebhookEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps a <c>POST</c> endpoint that verifies the <c>X-FoPost-Signature</c> header against
    /// <c>FoPost:WebhookSecret</c> and dispatches to every registered
    /// <see cref="IFoPostWebhookHandler"/>. An unsigned or mismatched delivery is answered
    /// <c>401</c> and no handler runs.
    /// </summary>
    /// <remarks>
    /// The endpoint is anonymous: the signature is its authentication, and the app's own
    /// scheme would reject FoPost. Call <c>RequireAuthorization()</c> on the returned builder
    /// if you want your policy applied on top.
    /// </remarks>
    public static RouteHandlerBuilder MapFoPostWebhook(
        this IEndpointRouteBuilder endpoints,
        string pattern = FoPostDefaults.WebhookPath)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints
            .MapPost(pattern, static (HttpContext context) => FoPostWebhookEndpoint.HandleAsync(context))
            .AllowAnonymous();
    }
}
