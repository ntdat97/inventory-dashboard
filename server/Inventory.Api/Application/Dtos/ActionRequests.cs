using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Application.Dtos;

/// <summary>Request body for logging a new action against a vehicle. The action starts life as <c>Proposed</c>.</summary>
public record CreateActionRequest(ActionType Type, decimal? ProposedValue, string Note);

/// <summary>Request body for a lifecycle transition. <see cref="Outcome"/> is required when moving to <c>Resolved</c>.</summary>
public record UpdateActionRequest(ActionStatus Status, ActionOutcome? Outcome);
