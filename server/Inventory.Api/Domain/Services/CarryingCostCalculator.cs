using Inventory.Api.Domain.Configuration;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Domain.Services;

/// <summary>Pure: acquisition cost/date (derived days from the injected clock) + config -> estimated carrying cost.</summary>
public class CarryingCostCalculator
{
    private readonly IClock _clock;
    private readonly CarryingCostConfig _config;

    public CarryingCostCalculator(IClock clock, IOptions<CarryingCostConfig> config)
    {
        _clock = clock;
        _config = config.Value;
    }

    public decimal CalculateDailyCost(decimal acquisitionCost)
    {
        var dailyInterest = acquisitionCost * (_config.AnnualInterestRate / 365m);
        var dailyDepreciation = acquisitionCost * _config.DailyDepreciationRate;
        return dailyInterest + dailyDepreciation + _config.FixedDailyHolding;
    }

    /// <summary>
    /// Carrying cost accrued from acquisition up to <paramref name="asOfUtc"/> (a closed vehicle's <c>ClosedDate</c>),
    /// or up to the current clock when null (still held). Freezing the anchor stops a sold unit's cost from growing forever.
    /// </summary>
    public decimal CalculateToDate(decimal acquisitionCost, DateTime acquisitionDateUtc, DateTime? asOfUtc = null)
    {
        var asOf = (asOfUtc ?? _clock.UtcNow).Date;
        var daysInInventory = Math.Max(0, (int)(asOf - acquisitionDateUtc.Date).TotalDays);
        return daysInInventory * CalculateDailyCost(acquisitionCost);
    }
}
