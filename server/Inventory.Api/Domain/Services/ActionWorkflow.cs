using Inventory.Api.Domain.Entities;
using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Domain.Services;

/// <summary>Pure state machine for the InventoryAction lifecycle: Proposed -> Approved -> InProgress -> Resolved.</summary>
public class ActionWorkflow
{
    private static readonly Dictionary<ActionStatus, ActionStatus[]> ValidTransitions = new()
    {
        [ActionStatus.Proposed] = [ActionStatus.Approved],
        [ActionStatus.Approved] = [ActionStatus.InProgress],
        [ActionStatus.InProgress] = [ActionStatus.Resolved],
        [ActionStatus.Resolved] = [],
    };

    private readonly IClock _clock;

    public ActionWorkflow(IClock clock)
    {
        _clock = clock;
    }

    public bool CanTransition(ActionStatus from, ActionStatus to) =>
        ValidTransitions[from].Contains(to);

    public ActionTransitionResult TryTransition(InventoryAction action, ActionStatus to, ActionOutcome? outcome = null)
    {
        if (!CanTransition(action.Status, to))
        {
            return ActionTransitionResult.Fail($"Cannot transition from {action.Status} to {to}.");
        }

        if (to == ActionStatus.Resolved && outcome is null)
        {
            return ActionTransitionResult.Fail("An outcome is required to resolve an action.");
        }

        action.Status = to;
        action.Outcome = outcome;
        action.UpdatedAt = _clock.UtcNow;

        return ActionTransitionResult.Ok();
    }
}
