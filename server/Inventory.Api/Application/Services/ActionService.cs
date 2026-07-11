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

    public ActionService(AppDbContext db, ActionWorkflow workflow, IClock clock)
    {
        _db = db;
        _workflow = workflow;
        _clock = clock;
    }

    public async Task<ActionResultOf> CreateAsync(Guid vehicleId, CreateActionRequest request, CancellationToken ct = default)
    {
        var vehicleExists = await _db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct);
        if (!vehicleExists)
        {
            return ActionResultOf.NotFound($"Vehicle {vehicleId} was not found.");
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
        var action = await _db.InventoryActions.FirstOrDefaultAsync(a => a.Id == actionId, ct);
        if (action is null)
        {
            return ActionResultOf.NotFound($"Action {actionId} was not found.");
        }

        var transition = _workflow.TryTransition(action, request.Status, request.Outcome);
        if (!transition.Success)
        {
            return ActionResultOf.Conflict(transition.Error!);
        }

        await _db.SaveChangesAsync(ct);
        return ActionResultOf.Ok(DtoMapper.ToActionDto(action));
    }
}
