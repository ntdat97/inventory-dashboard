using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Application.Recommendations;

/// <summary>
/// The LLM's parsed, typed enrichment. The rationale is the grounded action explanation; MarketRead is optional
/// analyst judgment based on vehicle identity/segment and must not be treated as a hard fact.
/// </summary>
public record AiRecommendation(ActionType Action, decimal? ProposedValue, string Rationale, string? MarketRead = null);

/// <summary>
/// Provider-agnostic seam for the LLM call, wrapped by a resilience handler in the composition root. Isolated so
/// tests use a fake (never the network) and the provider is swappable. Returns <c>null</c> when the AI is
/// unreachable/disabled; the caller degrades to baseline.
/// </summary>
public interface IAiClient
{
    Task<AiRecommendation?> EnrichAsync(RecommendationContext context, RecommendationResult baseline, CancellationToken ct);
}
