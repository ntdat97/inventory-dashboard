using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Application.Dtos;

/// <summary>A vehicle as it appears in the inventory grid: persisted fields + fields derived on read.</summary>
public record VehicleListItemDto(
    Guid Id,
    string Vin,
    Guid DealershipId,
    string Make,
    string Model,
    int Year,
    string? Trim,
    string? Color,
    int? Mileage,
    DateTime AcquisitionDate,
    decimal AcquisitionCost,
    decimal ListPrice,
    VehicleStatus Status,
    int DaysInInventory,
    AgingTier Tier,
    int? DaysUntilAging,
    decimal CarryingCostToDate);

/// <summary>Vehicle detail: everything in the list item plus the dealership name and full action history.</summary>
public record VehicleDetailDto(
    Guid Id,
    string Vin,
    Guid DealershipId,
    string? DealershipName,
    string Make,
    string Model,
    int Year,
    string? Trim,
    string? Color,
    int? Mileage,
    DateTime AcquisitionDate,
    decimal AcquisitionCost,
    decimal ListPrice,
    VehicleStatus Status,
    int DaysInInventory,
    AgingTier Tier,
    int? DaysUntilAging,
    decimal CarryingCostToDate,
    IReadOnlyList<ActionDto> History);

/// <summary>An inventory action as returned to the client.</summary>
public record ActionDto(
    Guid Id,
    Guid VehicleId,
    ActionType Type,
    ActionStatus Status,
    decimal? ProposedValue,
    string Note,
    ActionOutcome? Outcome,
    DateTime CreatedAt,
    DateTime UpdatedAt);
