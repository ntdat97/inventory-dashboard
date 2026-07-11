namespace Inventory.Api.Application.Recommendations;

/// <summary>Configuration for the AI recommendation feature (bound from the <c>Ai</c> config section).</summary>
public class AiOptions
{
    /// <summary>Master switch. When false (default) the LLM call is skipped entirely and the baseline is served.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>LLM HTTP endpoint. When empty, the client treats the provider as unconfigured and returns baseline.</summary>
    public string? Endpoint { get; set; }

    /// <summary>API key for the provider. Supplied via env/user-secrets, never committed.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Model identifier passed to the provider.</summary>
    public string? Model { get; set; }

    /// <summary>How long a per-vehicle recommendation is cached (cost/abuse control on the public demo).</summary>
    public int CacheMinutes { get; set; } = 10;

    /// <summary>Per-vehicle request budget per <see cref="RateLimitWindowSeconds"/> window.</summary>
    public int RateLimitPermitPerVehicle { get; set; } = 30;

    /// <summary>Length of the rate-limit window in seconds.</summary>
    public int RateLimitWindowSeconds { get; set; } = 60;
}
