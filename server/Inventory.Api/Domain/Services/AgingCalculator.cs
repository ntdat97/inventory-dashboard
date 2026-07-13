using Inventory.Api.Domain.Configuration;
using Inventory.Api.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Domain.Services;

/// <summary>Pure: days-in-inventory (derived from the injected clock) -> tier + days-until-aging.</summary>
public class AgingCalculator
{
    private readonly IClock _clock;
    private readonly AgingConfig _config;

    public AgingCalculator(IClock clock, IOptions<AgingConfig> config)
    {
        _clock = clock;
        _config = config.Value;
    }

    /// <summary>
    /// Days-in-inventory + tier as of a point in time. <paramref name="asOfUtc"/> freezes the clock for a vehicle that
    /// has left inventory (its <c>ClosedDate</c>): a closed unit's aging is a historical fact, not a live counter.
    /// Null (the default) means "still held" and derives against the current clock.
    /// </summary>
    public AgingResult Calculate(DateTime acquisitionDateUtc, DateTime? asOfUtc = null)
    {
        var asOf = (asOfUtc ?? _clock.UtcNow).Date;
        var daysInInventory = Math.Max(0, (int)(asOf - acquisitionDateUtc.Date).TotalDays);

        var tier = daysInInventory switch
        {
            var d when d <= _config.FreshMaxDays => AgingTier.Fresh,
            var d when d <= _config.WatchMaxDays => AgingTier.Watch,
            var d when d <= _config.AgingMaxDays => AgingTier.Aging,
            _ => AgingTier.Critical,
        };

        var daysUntilAging = daysInInventory <= _config.AgingMaxDays
            ? _config.AgingMaxDays - daysInInventory
            : (int?)null;

        return new AgingResult(daysInInventory, tier, daysUntilAging);
    }
}
