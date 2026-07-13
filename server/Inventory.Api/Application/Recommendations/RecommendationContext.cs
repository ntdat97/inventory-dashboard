using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Application.Recommendations;

/// <summary>
/// The factual signals a recommendation is grounded in — assembled from the vehicle + derived logic, never invented.
/// Passed to the baseline recommender and (as structured context) to the LLM so its wording stays grounded.
/// </summary>
public record RecommendationContext(
    int DaysInInventory,
    AgingTier Tier,
    decimal ListPrice,
    decimal AcquisitionCost,
    decimal CarryingCostToDate,
    int Year = 0,
    string Make = "",
    string Model = "",
    string? Trim = null,
    int? Mileage = null,
    decimal? AverageActiveDaysInInventory = null,
    decimal? AverageActiveCarryingCostToDate = null)
{
    /// <summary>Gross margin over cost basis at the current list price (negative if underwater).</summary>
    public decimal MarginRatio => AcquisitionCost <= 0 ? 0m : (ListPrice - AcquisitionCost) / AcquisitionCost;

    /// <summary>Dollar room between the current list price and the cost basis before the unit is underwater.</summary>
    public decimal MarginDollars => ListPrice - AcquisitionCost;

    /// <summary>Current estimated daily holding drag.</summary>
    public decimal DailyCarryingCost => DaysInInventory <= 0 ? 0m : CarryingCostToDate / DaysInInventory;
}

/// <summary>The deterministic baseline output: a concrete action, an optional proposed value, and a grounded rationale.</summary>
public record RecommendationResult(
    ActionType Action,
    decimal? ProposedValue,
    string Rationale,
    IReadOnlyList<string> GroundingFacts);
