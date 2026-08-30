using FoPost.AspNetCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers a health check that probes the FoPost API with the configured credential.</summary>
public static class FoPostHealthCheckExtensions
{
    /// <summary>
    /// Adds the FoPost health check. Call <c>AddFoPost</c> first — the check resolves the same
    /// client the rest of the app uses.
    /// </summary>
    public static IHealthChecksBuilder AddFoPostHealthCheck(
        this IServiceCollection services,
        string name = FoPostDefaults.HealthCheckName,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddHealthChecks().AddFoPost(name, failureStatus, tags, timeout);
    }

    /// <summary>Adds the FoPost health check to an existing health-checks builder.</summary>
    public static IHealthChecksBuilder AddFoPost(
        this IHealthChecksBuilder builder,
        string name = FoPostDefaults.HealthCheckName,
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddCheck<FoPostHealthCheck>(name, failureStatus, tags ?? [], timeout);
    }
}
