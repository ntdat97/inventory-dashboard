using Inventory.Api.Application.Auth;
using Inventory.Api.Application.Dtos;
using Inventory.Api.Domain.Entities;
using Inventory.Api.Domain.Enums;
using Inventory.Api.Domain.Services;
using Inventory.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Application.Services;

/// <summary>
/// Persists inventory actions and drives their lifecycle. New actions start as <c>Proposed</c>; transitions are
/// gated by the pure <see cref="ActionWorkflow"/> (an invalid transition is a Conflict, mapped to 409). Every
/// action is retained as immutable per-vehicle history.
/// </summary>
public class ActionService
{
    private readonly AppDbContext _db;
    private readonly ActionWorkflow _workflow;
    private readonly IClock _clock;
    private readonly ICurrentUserService _currentUser;

    public ActionService(AppDbContext db, ActionWorkflow workflow, IClock clock, ICurrentUserService currentUser)
    {
        _db = db;
        _workflow = workflow;
        _clock = clock;
        _currentUser = currentUser;
    }

    private Guid? ScopedDealershipId =>
        _currentUser.DealershipId is { } s && Guid.TryParse(s, out var id) ? id : null;

    public async Task<ActionResultOf> CreateAsync(Guid vehicleId, CreateActionRequest request, CancellationToken ct = default)
    {
        var vehicle = await _db.Vehicles.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vehicleId, ct);
        if (vehicle is null)
        {
            return ActionResultOf.NotFound($"Vehicle {vehicleId} was not found.");
        }

        // Enforce dealership scope: a manager can only act on their own dealership's vehicles.
        // Users without a dealership claim (platform admin) bypass this check.
        var scopedDealershipId = ScopedDealershipId;
        if (scopedDealershipId is not null && vehicle.DealershipId != scopedDealershipId)
        {
            return ActionResultOf.NotFound($"Vehicle {vehicleId} was not found.");
        }

        // A closed vehicle (sold/transferred/auctioned) has left the risk ledger: its action history is retained for
        // review but frozen. Logging a new action against it is a conflict, not a silent no-op.
        if (vehicle.Status.IsClosed())
        {
            return ActionResultOf.Conflict(
                $"Vehicle {vehicleId} is {vehicle.Status} and its action history is read-only.");
        }

        var now = _clock.UtcNow;
        var action = new InventoryAction
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            Type = request.Type,
            Status = ActionStatus.Proposed,
            ProposedValue = request.ProposedValue,
            Note = request.Note,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.InventoryActions.Add(action);
        await _db.SaveChangesAsync(ct);

        return ActionResultOf.Ok(DtoMapper.ToActionDto(action));
    }

    public async Task<ActionResultOf> TransitionAsync(Guid actionId, UpdateActionRequest request, CancellationToken ct = default)
    {
        var action = await _db.InventoryActions
            .Include(a => a.Vehicle)
            .FirstOrDefaultAsync(a => a.Id == actionId, ct);
        if (action is null)
        {
            return ActionResultOf.NotFound($"Action {actionId} was not found.");
        }

        // Enforce dealership scope on the owning vehicle, same rule as CreateAsync.
        var scopedDealershipId = ScopedDealershipId;
        if (scopedDealershipId is not null
            && action.Vehicle is not null
            && action.Vehicle.DealershipId != scopedDealershipId)
        {
            return ActionResultOf.NotFound($"Action {actionId} was not found.");
        }

        // History on a closed vehicle is frozen — advancing an action on a sold/transferred/auctioned unit is a conflict.
        if (action.Vehicle is not null && action.Vehicle.Status.IsClosed())
        {
            return ActionResultOf.Conflict(
                $"Vehicle {action.VehicleId} is {action.Vehicle.Status} and its action history is read-only.");
        }

        var transition = _workflow.TryTransition(action, request.Status, request.Outcome);
        if (!transition.Success)
        {
            return ActionResultOf.Conflict(transition.Error!);
        }

        // Starting a price-reduction action is the moment the dealership actually changes the advertised price.
        // Proposed/Approved are planning states; InProgress means the new listing price must be reflected everywhere.
        if (action is
            {
                Type: ActionType.PriceReduction,
                Status: ActionStatus.InProgress,
                ProposedValue: > 0,
                Vehicle: not null
            })
        {
            action.Vehicle.ListPrice = action.ProposedValue.Value;
        }

        // Closing a vehicle is a consequence of a deal landing, never a standalone flip: an action that resolves as
        // Sold takes its owning unit out of active inventory. *Which* exit lane (retail / transfer / auction) is the
        // action's type; the "did it leave at all?" is the outcome. A NotSold resolution just closes the task and
        // leaves the unit in stock. The IsClosed() gate above guarantees the vehicle is still active here.
        if (action is { Status: ActionStatus.Resolved, Outcome: ActionOutcome.Sold, Vehicle: not null })
        {
            action.Vehicle.Close(action.Type.SaleDestination(), _clock.UtcNow.Date);
        }

        await _db.SaveChangesAsync(ct);
        return ActionResultOf.Ok(DtoMapper.ToActionDto(action));
    }
}
