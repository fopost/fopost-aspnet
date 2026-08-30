using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FoPost.AspNetCore;

/// <summary>
/// Reads the workspaces the configured credential can see. It is the cheapest authenticated
/// call the API offers, so it proves reachability and the credential in one request.
/// </summary>
/// <remarks>
/// Descriptions never carry the credential, the base URL, or a provider exception message —
/// health endpoints are routinely exposed to infrastructure that is not trusted with them.
/// </remarks>
internal sealed class FoPostHealthCheck : IHealthCheck
{
    private readonly FoPostClient _client;

    public FoPostHealthCheck(FoPostClient client) => _client = client;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var workspaces = await _client.Workspaces.ListAsync(cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy($"FoPost API reachable; {workspaces.Count} workspace(s) visible.");
        }
        catch (FoPostAuthenticationException)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "FoPost rejected the configured credential.");
        }
        catch (FoPostRateLimitException)
        {
            return HealthCheckResult.Degraded("FoPost API reachable but rate limiting this credential.");
        }
        catch (FoPostException error)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                $"FoPost API answered HTTP {error.Status}.");
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "FoPost API unreachable.");
        }
    }
}
