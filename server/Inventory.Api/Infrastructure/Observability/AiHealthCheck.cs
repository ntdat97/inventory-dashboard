using Inventory.Api.Application.Recommendations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Infrastructure.Observability;

/// <summary>
/// Readiness check for the AI provider. By design the AI is a <em>degradable</em> dependency: the recommendation
/// feature always falls back to the deterministic baseline, so an AI that is disabled or unconfigured reports
/// <see cref="HealthStatus.Degraded"/> — never <see cref="HealthStatus.Unhealthy"/> — and readiness stays 200 (design §9).
/// </summary>
public class AiHealthCheck : IHealthCheck
{
    private readonly AiOptions _options;

    public AiHealthCheck(IOptions<AiOptions> options)
    {
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            return Task.FromResult(HealthCheckResult.Degraded(
                "AI provider disabled or unconfigured; recommendations serve the baseline."));
        }

        // Reachability is not probed on the hot readiness path (no outbound call): the provider is configured, and
        // the recommendation path degrades gracefully at call time if it is in fact unreachable.
        return Task.FromResult(HealthCheckResult.Healthy("AI provider configured."));
    }
}
