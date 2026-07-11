using Inventory.Api.Domain.Services;

namespace Inventory.Tests.TestSupport;

public class FakeClock : IClock
{
    public FakeClock(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}
