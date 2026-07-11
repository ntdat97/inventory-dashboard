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

    public AgingResult Calculate(DateTime acquisitionDateUtc)
    {
        var daysInInventory = (int)(_clock.UtcNow.Date - acquisitionDateUtc.Date).TotalDays;

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
