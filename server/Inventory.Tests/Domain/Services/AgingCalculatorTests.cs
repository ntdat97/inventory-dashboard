using FluentAssertions;
using Inventory.Api.Domain.Configuration;
using Inventory.Api.Domain.Enums;
using Inventory.Api.Domain.Services;
using Inventory.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace Inventory.Tests.Domain.Services;

public class AgingCalculatorTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

    private static AgingCalculator CreateCalculator(AgingConfig? config = null) =>
        new(new FakeClock(Now), Options.Create(config ?? new AgingConfig()));

    [Theory]
    [InlineData(0, AgingTier.Fresh)]
    [InlineData(29, AgingTier.Fresh)]
    [InlineData(30, AgingTier.Fresh)] // exactly-at-threshold: still Fresh (0-30 inclusive)
    [InlineData(31, AgingTier.Watch)] // one day past the Fresh boundary
    [InlineData(59, AgingTier.Watch)]
    [InlineData(60, AgingTier.Watch)] // exactly-at-threshold: still Watch (31-60 inclusive)
    [InlineData(61, AgingTier.Aging)] // one day past the Watch boundary
    [InlineData(89, AgingTier.Aging)]
    [InlineData(90, AgingTier.Aging)] // exactly-at-threshold: still Aging (61-90 inclusive)
    [InlineData(91, AgingTier.Critical)] // one day past the Aging boundary
    [InlineData(200, AgingTier.Critical)]
    public void Calculate_AssignsExpectedTier_ForDaysInInventory(int days, AgingTier expectedTier)
    {
        var calculator = CreateCalculator();
        var acquisitionDate = Now.AddDays(-days);

        var result = calculator.Calculate(acquisitionDate);

        result.DaysInInventory.Should().Be(days);
        result.Tier.Should().Be(expectedTier);
    }

    [Theory]
    [InlineData(0, 90)]
    [InlineData(30, 60)]
    [InlineData(60, 30)]
    [InlineData(89, 1)]
    [InlineData(90, 0)] // exactly at the aging threshold: zero days remaining, not yet null
    public void Calculate_ReturnsDaysUntilAging_WhileNotYetAged(int days, int expectedDaysUntilAging)
    {
        var calculator = CreateCalculator();

        var result = calculator.Calculate(Now.AddDays(-days));

        result.DaysUntilAging.Should().Be(expectedDaysUntilAging);
    }

    [Theory]
    [InlineData(91)]
    [InlineData(150)]
    public void Calculate_ReturnsNullDaysUntilAging_OnceCritical(int days)
    {
        var calculator = CreateCalculator();

        var result = calculator.Calculate(Now.AddDays(-days));

        result.DaysUntilAging.Should().BeNull();
    }

    [Fact]
    public void Calculate_UsesCustomConfig_ForTierBoundaries()
    {
        var config = new AgingConfig { FreshMaxDays = 10, WatchMaxDays = 20, AgingMaxDays = 30 };
        var calculator = CreateCalculator(config);

        calculator.Calculate(Now.AddDays(-10)).Tier.Should().Be(AgingTier.Fresh);
        calculator.Calculate(Now.AddDays(-11)).Tier.Should().Be(AgingTier.Watch);
        calculator.Calculate(Now.AddDays(-31)).Tier.Should().Be(AgingTier.Critical);
    }

    [Fact]
    public void Calculate_WithAsOf_FreezesDaysAtThatDate_NotTheClock()
    {
        var calculator = CreateCalculator();
        var acquisition = Now.AddDays(-100);
        var closed = Now.AddDays(-40); // left inventory 40 days ago after 60 days in stock

        var result = calculator.Calculate(acquisition, closed);

        // Frozen at 60 days (closed - acquisition), NOT 100 (now - acquisition).
        result.DaysInInventory.Should().Be(60);
        result.Tier.Should().Be(AgingTier.Watch);
    }

    [Fact]
    public void Calculate_WithNullAsOf_MatchesTheClock()
    {
        var calculator = CreateCalculator();
        var acquisition = Now.AddDays(-100);

        calculator.Calculate(acquisition, null).DaysInInventory
            .Should().Be(calculator.Calculate(acquisition).DaysInInventory);
    }
}
