using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Application.Recommendations;

/// <summary>
/// The LLM's parsed, typed enrichment. Kept deliberately small: the AI may refine the recommended action, the
/// proposed value and the rationale wording, but its output is validated before use and falls back to baseline if invalid.
/// </summary>
public record AiRecommendation(ActionType Action, decimal? ProposedValue, string Rationale);

/// <summary>
/// Provider-agnostic seam for the LLM call, wrapped by a resilience handler in the composition root. Isolated so
/// tests use a fake (never the network) and the provider is swappable. Returns <c>null</c> when the AI is
/// unreachable/disabled; the caller degrades to baseline.
/// </summary>
public interface IAiClient
{
    Task<AiRecommendation?> EnrichAsync(RecommendationContext context, RecommendationResult baseline, CancellationToken ct);
}
