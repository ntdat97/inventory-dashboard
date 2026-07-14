using Inventory.Api.Application.Auth;
using Inventory.Api.Application.Dtos;
using Inventory.Api.Domain.Entities;
using Inventory.Api.Domain.Services;
using Inventory.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Application.Services;

public class VehicleReservationService
{
    private readonly AppDbContext _db;
    private readonly AgingCalculator _aging;
    private readonly CarryingCostCalculator _carryingCost;
    private readonly ICurrentUserService _currentUser;

    public VehicleReservationService(
        AppDbContext db,
        AgingCalculator aging,
        CarryingCostCalculator carryingCost,
        ICurrentUserService currentUser)
    {
        _db = db;
        _aging = aging;
        _carryingCost = carryingCost;
        _currentUser = currentUser;
    }

    private Guid? ScopedDealershipId =>
        _currentUser.DealershipId is { } s && Guid.TryParse(s, out var id) ? id : null;

    public Task<VehicleMutationResultOf> ReserveAsync(Guid vehicleId, CancellationToken ct = default) =>
        ChangeStatusAsync(
            vehicleId,
            v => v.Reserve(),
            $"Vehicle {vehicleId} can only be reserved from InStock.",
            ct);

    public Task<VehicleMutationResultOf> ReleaseAsync(Guid vehicleId, CancellationToken ct = default) =>
        ChangeStatusAsync(
            vehicleId,
            v => v.ReleaseReservation(),
            $"Vehicle {vehicleId} can only release a reservation from Reserved.",
            ct);

    private async Task<VehicleMutationResultOf> ChangeStatusAsync(
        Guid vehicleId,
        Action<Vehicle> changeStatus,
        string conflictMessage,
        CancellationToken ct)
    {
        var vehicle = await _db.Vehicles
            .Include(v => v.Dealership)
            .Include(v => v.Actions)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, ct);

        if (vehicle is null)
        {
            return VehicleMutationResultOf.NotFound($"Vehicle {vehicleId} was not found.");
        }

        // Enforce dealership scope: a manager can only reserve/release their own dealership's vehicles.
        var scopedDealershipId = ScopedDealershipId;
        if (scopedDealershipId is not null && vehicle.DealershipId != scopedDealershipId)
        {
            return VehicleMutationResultOf.NotFound($"Vehicle {vehicleId} was not found.");
        }

        try
        {
            changeStatus(vehicle);
        }
        catch (InvalidOperationException)
        {
            return VehicleMutationResultOf.Conflict(conflictMessage);
        }

        await _db.SaveChangesAsync(ct);
        return VehicleMutationResultOf.Ok(DtoMapper.ToVehicleDetailDto(vehicle, _aging, _carryingCost));
    }
}

public record VehicleMutationResultOf(ServiceStatus Status, VehicleDetailDto? Vehicle = null, string? Error = null)
{
    public static VehicleMutationResultOf Ok(VehicleDetailDto vehicle) => new(ServiceStatus.Success, vehicle);
    public static VehicleMutationResultOf NotFound(string error) => new(ServiceStatus.NotFound, Error: error);
    public static VehicleMutationResultOf Conflict(string error) => new(ServiceStatus.Conflict, Error: error);
}
