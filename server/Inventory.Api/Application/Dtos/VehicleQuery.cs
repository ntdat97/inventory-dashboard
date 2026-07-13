using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Application.Dtos;

/// <summary>
/// Bound from the <c>GET /vehicles</c> query string: filtering, sorting and pagination inputs.
/// Sort accepts <c>daysInInventory</c>, <c>listPrice</c>, <c>make</c>, <c>year</c>, <c>carryingCost</c>,
/// optionally prefixed with <c>-</c> for descending (default: <c>-daysInInventory</c>, most-aged first).
/// </summary>
public class VehicleQuery
{
    public Guid? DealershipId { get; set; }

    /// <summary>
    /// Free-text search matched (case-insensitive, substring) across make, model, trim, VIN, colour and year —
    /// a single "search the whole row" box for the dashboard, replacing the separate exact-match make/model inputs.
    /// </summary>
    public string? Search { get; set; }

    public string? Make { get; set; }
    public string? Model { get; set; }
    public VehicleScope Scope { get; set; } = VehicleScope.Active;

    /// <summary>
    /// Aging tiers to include (OR-combined). Repeat the key to select several — <c>?tier=Aging&amp;tier=Critical</c>.
    /// A single <c>?tier=Critical</c> binds to a one-element list, so callers passing one value still work. Empty = all tiers.
    /// </summary>
    public List<AgingTier> Tier { get; set; } = new();

    /// <summary>
    /// Statuses to include (OR-combined), same repeat-the-key convention as <see cref="Tier"/>. Empty = every status in scope.
    /// </summary>
    public List<VehicleStatus> Status { get; set; } = new();
    public int? MinDays { get; set; }
    public int? MaxDays { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
