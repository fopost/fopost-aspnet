using FoPost;
using FoPost.AspNetCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the handler <c>MapFoPostArticles</c> hands published articles to.</summary>
public static class FoPostArticleServiceCollectionExtensions
{
    /// <summary>Registers <typeparamref name="THandler"/> as the scoped article handler.</summary>
    public static IServiceCollection AddFoPostArticleHandler<THandler>(this IServiceCollection services)
        where THandler : class, IFoPostArticlePublishedHandler
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IFoPostArticlePublishedHandler, THandler>();

        return services;
    }

    /// <summary>
    /// Registers a delegate as the article handler. It may return the URL the article was
    /// stored at, or <c>null</c>.
    /// </summary>
    public static IServiceCollection AddFoPostArticleHandler(
        this IServiceCollection services,
        Func<BlogArticlePublishedPayload, CancellationToken, Task<string?>> onPublished)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(onPublished);

        services.TryAddSingleton<IFoPostArticlePublishedHandler>(new DelegateArticleHandler(onPublished));

        return services;
    }

    private sealed class DelegateArticleHandler : IFoPostArticlePublishedHandler
    {
        private readonly Func<BlogArticlePublishedPayload, CancellationToken, Task<string?>> _onPublished;

        public DelegateArticleHandler(Func<BlogArticlePublishedPayload, CancellationToken, Task<string?>> onPublished) =>
            _onPublished = onPublished;

        public Task<string?> HandleAsync(BlogArticlePublishedPayload article, CancellationToken cancellationToken) =>
            _onPublished(article, cancellationToken);
    }
}
