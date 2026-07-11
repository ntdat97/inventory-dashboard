using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Application.Dtos;

/// <summary>Capital-at-risk KPI payload for the dashboard. Computed over held inventory (InStock/Reserved).</summary>
public record InventorySummaryDto(
    int TotalUnits,
    decimal TotalInventoryValue,
    int AgedUnits,
    decimal AgedPercent,
    decimal CapitalTiedInAged,
    decimal AvgDaysInInventory,
    decimal TotalCarryingCostToDate,
    IReadOnlyDictionary<AgingTier, int> TierBreakdown);
