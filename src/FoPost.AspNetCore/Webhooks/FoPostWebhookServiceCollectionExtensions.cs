using FoPost.AspNetCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers webhook handlers for <c>MapFoPostWebhook</c> to dispatch to.</summary>
public static class FoPostWebhookServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="THandler"/> as a scoped
    /// <see cref="IFoPostWebhookHandler"/>. Registering the same type twice is a no-op, so
    /// calling this from a library and from the app is safe.
    /// </summary>
    public static IServiceCollection AddFoPostWebhookHandler<THandler>(this IServiceCollection services)
        where THandler : class, IFoPostWebhookHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IFoPostWebhookHandler, THandler>());

        return services;
    }
}
