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
    public string? Make { get; set; }
    public string? Model { get; set; }
    public AgingTier? Tier { get; set; }
    public VehicleStatus? Status { get; set; }
    public int? MinDays { get; set; }
    public int? MaxDays { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
