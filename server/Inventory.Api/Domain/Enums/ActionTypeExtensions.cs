namespace Inventory.Api.Domain.Enums;

/// <summary>
/// Maps an action type to the exit lane a vehicle takes when that action resolves as a closed deal
/// (<see cref="ActionOutcome.Sold"/>). A Transfer settles the unit to another store, an Auction sends it to the
/// block, and every other type is an ordinary retail sale. This is the single place that decides *which* closed
/// status a resolved deal produces — the "did it leave at all?" question is the outcome, kept separate.
/// </summary>
public static class ActionTypeExtensions
{
    public static VehicleStatus SaleDestination(this ActionType type) => type switch
    {
        ActionType.Transfer => VehicleStatus.Transferred,
        ActionType.Auction => VehicleStatus.AtAuction,
        _ => VehicleStatus.Sold,
    };
}
