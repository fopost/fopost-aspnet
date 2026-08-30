using FoPost;
using FoPost.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the FoPost client and its options in the service container.</summary>
public static class FoPostServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FoPostClient"/>, binding <see cref="FoPostOptions"/> from the
    /// <c>FoPost</c> section of the application's configuration.
    /// </summary>
    public static IServiceCollection AddFoPost(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddFoPostCore(builder => builder.BindConfiguration(FoPostOptions.SectionName));
    }

    /// <summary>
    /// Registers <see cref="FoPostClient"/> with options configured in code. Nothing is read
    /// from configuration, so this works in a bare container with no <see cref="IConfiguration"/>.
    /// </summary>
    public static IServiceCollection AddFoPost(this IServiceCollection services, Action<FoPostOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddFoPostCore(builder => builder.Configure(configure));
    }

    /// <summary>
    /// Registers <see cref="FoPostClient"/>, binding <see cref="FoPostOptions"/> from
    /// <paramref name="configuration"/>. Pass the root configuration and its <c>FoPost</c>
    /// section is used; pass a section and it is bound directly.
    /// </summary>
    public static IServiceCollection AddFoPost(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<FoPostOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration as IConfigurationSection ?? configuration.GetSection(FoPostOptions.SectionName);

        return services.AddFoPostCore(builder =>
        {
            builder.Bind(section);
            if (configure is not null)
            {
                builder.Configure(configure);
            }
        });
    }

    private static IServiceCollection AddFoPostCore(
        this IServiceCollection services,
        Action<OptionsBuilder<FoPostOptions>> bind)
    {
        var builder = services.AddOptions<FoPostOptions>();
        bind(builder);

        builder
            // The SDK's own contract: fall back to the environment when nothing is configured.
            .PostConfigure(static options =>
            {
                if (string.IsNullOrWhiteSpace(options.ApiKey) && string.IsNullOrWhiteSpace(options.BearerToken))
                {
                    options.ApiKey = Environment.GetEnvironmentVariable(FoPostOptions.ApiKeyEnvironmentVariable);
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddHttpClient(FoPostDefaults.HttpClientName)
            .ConfigureHttpClient(static (provider, http) =>
                http.Timeout = provider.GetRequiredService<IOptions<FoPostOptions>>().Value.Timeout)
            // FoPostClient is a singleton, so it holds its HttpClient for the life of the app and
            // handler rotation would never happen. Pool the connections instead, which is what
            // keeps DNS from going stale.
            .SetHandlerLifetime(System.Threading.Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            });

        services.TryAddSingleton<FoPostClient>(static provider =>
        {
            var options = provider.GetRequiredService<IOptions<FoPostOptions>>().Value;
            var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(FoPostDefaults.HttpClientName);

            return new FoPostClient(options.ToClientOptions(http));
        });

        return services;
    }
}
