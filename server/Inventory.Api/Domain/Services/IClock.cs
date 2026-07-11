namespace Inventory.Api.Domain.Services;

public interface IClock
{
    DateTime UtcNow { get; }
}
