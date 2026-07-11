namespace Inventory.Api.Domain.Services;

public record ActionTransitionResult(bool Success, string? Error = null)
{
    public static ActionTransitionResult Ok() => new(true);
    public static ActionTransitionResult Fail(string error) => new(false, error);
}
