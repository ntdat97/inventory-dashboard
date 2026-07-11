namespace Inventory.Api.Domain.Configuration;

/// <summary>Defaults per SYSTEM-DESIGN A4: dailyCost = acquisitionCost * (apr/365) + dailyDepreciation + fixedDailyHolding.</summary>
public class CarryingCostConfig
{
    public decimal AnnualInterestRate { get; set; } = 0.09m;
    public decimal DailyDepreciationRate { get; set; } = 0.0004m;
    public decimal FixedDailyHolding { get; set; } = 4m;
}
