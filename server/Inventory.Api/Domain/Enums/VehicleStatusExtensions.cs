namespace Inventory.Api.Domain.Enums;

/// <summary>
/// Single source of truth for the active-vs-closed split. "Active" units (InStock, Reserved) are still capital-at-risk,
/// so their aging + carrying cost accrue live; "closed" units (Sold, Transferred, AtAuction) have left the risk ledger,
/// so their metrics are frozen at <see cref="Entities.Vehicle.ClosedDate"/> and their action history is read-only.
/// </summary>
public static class VehicleStatusExtensions
{
    public static bool IsClosed(this VehicleStatus status) => status switch
    {
        VehicleStatus.Sold or VehicleStatus.Transferred or VehicleStatus.AtAuction => true,
        _ => false,
    };

    public static bool IsActive(this VehicleStatus status) => !status.IsClosed();
}
