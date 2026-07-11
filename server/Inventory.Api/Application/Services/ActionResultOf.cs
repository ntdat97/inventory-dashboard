using Inventory.Api.Application.Dtos;

namespace Inventory.Api.Application.Services;

/// <summary>Outcome of a service operation, mapped to HTTP status codes at the controller boundary.</summary>
public enum ServiceStatus
{
    Success,
    NotFound,
    Conflict,
}

/// <summary>Result of an action operation: the resulting DTO on success, or a reason (NotFound/Conflict) + message.</summary>
public record ActionResultOf(ServiceStatus Status, ActionDto? Action = null, string? Error = null)
{
    public static ActionResultOf Ok(ActionDto action) => new(ServiceStatus.Success, action);
    public static ActionResultOf NotFound(string error) => new(ServiceStatus.NotFound, Error: error);
    public static ActionResultOf Conflict(string error) => new(ServiceStatus.Conflict, Error: error);
}
