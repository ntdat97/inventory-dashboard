using FluentAssertions;
using Inventory.Api.Domain.Configuration;
using Inventory.Api.Domain.Services;
using Inventory.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace Inventory.Tests.Domain.Services;

public class CarryingCostCalculatorTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

    private static CarryingCostCalculator CreateCalculator(CarryingCostConfig? config = null) =>
        new(new FakeClock(Now), Options.Create(config ?? new CarryingCostConfig()));

    [Fact]
    public void CalculateDailyCost_MatchesFormula_FromDefaultConfig()
    {
        var calculator = CreateCalculator();
        const decimal acquisitionCost = 24000m;
        var config = new CarryingCostConfig();

        var expectedDaily = acquisitionCost * (config.AnnualInterestRate / 365m)
            + acquisitionCost * config.DailyDepreciationRate
            + config.FixedDailyHolding;

        calculator.CalculateDailyCost(acquisitionCost).Should().Be(expectedDaily);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(120)]
    public void CalculateToDate_AccruesLinearlyWithDaysInInventory(int days)
    {
        var calculator = CreateCalculator();
        const decimal acquisitionCost = 24000m;

        var toDate = calculator.CalculateToDate(acquisitionCost, Now.AddDays(-days));
        var dailyCost = calculator.CalculateDailyCost(acquisitionCost);

        toDate.Should().Be(days * dailyCost);
    }

    [Fact]
    public void CalculateToDate_IsZero_WhenAcquiredToday()
    {
        var calculator = CreateCalculator();

        calculator.CalculateToDate(24000m, Now).Should().Be(0m);
    }

    [Fact]
    public void CalculateToDate_WithAsOf_FreezesAccrualAtThatDate()
    {
        var calculator = CreateCalculator();
        const decimal acquisitionCost = 24000m;
        var acquisition = Now.AddDays(-100);
        var closed = Now.AddDays(-40); // 60 held-days, then left the lot

        var frozen = calculator.CalculateToDate(acquisitionCost, acquisition, closed);

        // Accrues over 60 days (acquisition -> closed), not 100 (acquisition -> now).
        frozen.Should().Be(60 * calculator.CalculateDailyCost(acquisitionCost));
        frozen.Should().BeLessThan(calculator.CalculateToDate(acquisitionCost, acquisition));
    }

    [Fact]
    public void CalculateToDate_UsesConfigVariant_HigherInterestAndDepreciation()
    {
        var config = new CarryingCostConfig
        {
            AnnualInterestRate = 0.15m,
            DailyDepreciationRate = 0.001m,
            FixedDailyHolding = 10m,
        };
        var calculator = CreateCalculator(config);
        const decimal acquisitionCost = 30000m;
        const int days = 45;

        var expectedDaily = acquisitionCost * (config.AnnualInterestRate / 365m)
            + acquisitionCost * config.DailyDepreciationRate
            + config.FixedDailyHolding;

        calculator.CalculateToDate(acquisitionCost, Now.AddDays(-days)).Should().Be(days * expectedDaily);
    }

    [Fact]
    public void CalculateToDate_UsesConfigVariant_ZeroInterestAndDepreciation()
    {
        var config = new CarryingCostConfig
        {
            AnnualInterestRate = 0m,
            DailyDepreciationRate = 0m,
            FixedDailyHolding = 4m,
        };
        var calculator = CreateCalculator(config);

        calculator.CalculateToDate(30000m, Now.AddDays(-10)).Should().Be(40m);
    }
}
