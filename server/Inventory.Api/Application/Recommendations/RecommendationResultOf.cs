using Inventory.Api.Application.Dtos;
using Inventory.Api.Application.Services;

namespace Inventory.Api.Application.Recommendations;

/// <summary>
/// Result of a recommendation lookup, mapped to HTTP at the controller boundary: the DTO on success, NotFound (404)
/// for an unknown vehicle, or Conflict (409) for a closed vehicle (recommendations apply to active inventory only).
/// Reuses the shared <see cref="ServiceStatus"/> so the controllers map every service outcome the same way.
/// </summary>
public record RecommendationResultOf(ServiceStatus Status, RecommendationDto? Recommendation = null, string? Error = null)
{
    public static RecommendationResultOf Ok(RecommendationDto dto) => new(ServiceStatus.Success, dto);
    public static RecommendationResultOf NotFound(string error) => new(ServiceStatus.NotFound, Error: error);
    public static RecommendationResultOf Conflict(string error) => new(ServiceStatus.Conflict, Error: error);
}
