using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Domain.Entities;

public class InventoryAction
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public ActionType Type { get; set; }
    public ActionStatus Status { get; set; }
    public decimal? ProposedValue { get; set; }
    public string Note { get; set; } = string.Empty;
    public ActionOutcome? Outcome { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Vehicle? Vehicle { get; set; }
}
