using System.Net.Http.Json;
using FluentAssertions;
using Inventory.Api.Application.Dtos;
using Inventory.Api.Domain.Enums;
using Inventory.Tests.TestSupport;

namespace Inventory.Tests.Integration;

public class InventorySummaryTests : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client;

    public InventorySummaryTests(InventoryApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // The seed (days-in-inventory anchored to the fixed test clock, all InStock).
    private static readonly (int Days, decimal Cost, decimal ListPrice)[] Seed =
    [
        (5, 24000m, 27500m), (18, 21000m, 24000m), (30, 26000m, 29500m), (38, 38000m, 43000m),
        (52, 25000m, 28500m), (60, 27000m, 30500m), (66, 24500m, 27800m), (80, 19500m, 22800m),
        (90, 23000m, 26200m), (95, 18500m, 21600m), (130, 31000m, 34500m), (210, 29000m, 33200m),
    ];

    [Fact]
    public async Task Summary_AggregatesMatchTheSeed()
    {
        var summary = await _client.GetFromJsonAsync<InventorySummaryDto>("/api/inventory/summary", JsonDefaults.Options);

        summary!.TotalUnits.Should().Be(12);
        summary.TotalInventoryValue.Should().Be(349100m);      // sum of list prices
        summary.AgedUnits.Should().Be(3);                       // Critical: days 95/130/210
        summary.AgedPercent.Should().Be(25.00m);               // 3 / 12
        summary.CapitalTiedInAged.Should().Be(78500m);         // cost basis of the 3 critical units
        summary.AvgDaysInInventory.Should().Be(72.83m);        // mean of the day offsets, 2dp

        summary.TierBreakdown[AgingTier.Fresh].Should().Be(3);
        summary.TierBreakdown[AgingTier.Watch].Should().Be(3);
        summary.TierBreakdown[AgingTier.Aging].Should().Be(3);
        summary.TierBreakdown[AgingTier.Critical].Should().Be(3);
    }

    [Fact]
    public async Task Summary_TotalCarryingCost_MatchesA4Formula()
    {
        var summary = await _client.GetFromJsonAsync<InventorySummaryDto>("/api/inventory/summary", JsonDefaults.Options);

        // Independent re-derivation of the A4 model: dailyCost = cost*(apr/365) + cost*dailyDep + fixed.
        const decimal apr = 0.09m, dailyDep = 0.0004m, fixedDaily = 4m;
        var expected = Seed.Sum(s => s.Days * (s.Cost * (apr / 365m) + s.Cost * dailyDep + fixedDaily));

        summary!.TotalCarryingCostToDate.Should().Be(expected);
    }
}
