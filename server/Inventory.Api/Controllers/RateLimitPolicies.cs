namespace Inventory.Api.Controllers;

/// <summary>Named rate-limiting policies applied to controllers/actions.</summary>
public static class RateLimitPolicies
{
    /// <summary>Per-vehicle limit on the recommendation endpoint — a cost/abuse guard on the public demo (design §8).</summary>
    public const string VehicleRecommendation = "vehicle-recommendation";
}
