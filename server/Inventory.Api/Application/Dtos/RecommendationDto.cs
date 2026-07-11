using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Application.Dtos;

/// <summary>Where a recommendation's wording/action came from: the deterministic baseline or the LLM enrichment.</summary>
public enum RecommendationSource
{
    Baseline,
    Ai,
}

/// <summary>
/// An action recommendation for a vehicle. The baseline is always present; when the LLM is reachable and its
/// output validates, <see cref="Source"/> is <see cref="RecommendationSource.Ai"/> and the rationale is enriched.
/// </summary>
public record RecommendationDto(
    Guid VehicleId,
    ActionType RecommendedAction,
    decimal? ProposedValue,
    string Rationale,
    RecommendationSource Source,
    IReadOnlyList<string> GroundingFacts);
