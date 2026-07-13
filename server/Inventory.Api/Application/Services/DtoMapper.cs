using Inventory.Api.Application.Dtos;
using Inventory.Api.Domain.Entities;
using Inventory.Api.Domain.Services;

namespace Inventory.Api.Application.Services;

/// <summary>Entity -> DTO mapping shared by the read and write services.</summary>
internal static class DtoMapper
{
    public static ActionDto ToActionDto(InventoryAction a) => new(
        a.Id, a.VehicleId, a.Type, a.Status, a.ProposedValue, a.Note, a.Outcome, a.CreatedAt, a.UpdatedAt);

    public static VehicleListItemDto ToVehicleListItemDto(
        Vehicle v,
        AgingCalculator agingCalculator,
        CarryingCostCalculator carryingCostCalculator)
    {
        var aging = agingCalculator.Calculate(v.AcquisitionDate, v.ClosedDate);
        var carryingCost = carryingCostCalculator.CalculateToDate(v.AcquisitionCost, v.AcquisitionDate, v.ClosedDate);

        return new VehicleListItemDto(
            v.Id, v.Vin, v.DealershipId, v.Make, v.Model, v.Year, v.Trim, v.Color, v.Mileage,
            v.AcquisitionDate, v.AcquisitionCost, v.ListPrice, v.Status, v.ClosedDate,
            aging.DaysInInventory, aging.Tier, aging.DaysUntilAging, carryingCost);
    }

    public static VehicleDetailDto ToVehicleDetailDto(
        Vehicle v,
        AgingCalculator agingCalculator,
        CarryingCostCalculator carryingCostCalculator)
    {
        var aging = agingCalculator.Calculate(v.AcquisitionDate, v.ClosedDate);
        var carryingCost = carryingCostCalculator.CalculateToDate(v.AcquisitionCost, v.AcquisitionDate, v.ClosedDate);
        var history = v.Actions
            .OrderByDescending(a => a.CreatedAt)
            .Select(ToActionDto)
            .ToList();

        return new VehicleDetailDto(
            v.Id, v.Vin, v.DealershipId, v.Dealership?.Name,
            v.Make, v.Model, v.Year, v.Trim, v.Color, v.Mileage,
            v.AcquisitionDate, v.AcquisitionCost, v.ListPrice, v.Status, v.ClosedDate,
            aging.DaysInInventory, aging.Tier, aging.DaysUntilAging, carryingCost, history);
    }
}
