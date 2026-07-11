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

    public decimal CalculateToDate(decimal acquisitionCost, DateTime acquisitionDateUtc)
    {
        var daysInInventory = Math.Max(0, (int)(_clock.UtcNow.Date - acquisitionDateUtc.Date).TotalDays);
        return daysInInventory * CalculateDailyCost(acquisitionCost);
    }
}
