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

    // Active seed only: summary KPIs describe capital currently at risk (InStock + Reserved).
    private static readonly (int Days, decimal Cost, decimal ListPrice)[] Seed =
    [
        (3, 24000m, 27500m), (7, 39000m, 44500m), (18, 47000m, 53500m),
        (24, 27000m, 30500m), (30, 26000m, 29500m), (38, 38000m, 43000m),
        (60, 27000m, 30500m), (66, 24500m, 27800m), (80, 19500m, 22800m),
        (85, 46000m, 51900m), (90, 23000m, 26200m), (128, 34000m, 38200m),
    ];

    [Fact]
    public async Task Summary_AggregatesMatchTheSeed()
    {
        var summary = await _client.GetFromJsonAsync<InventorySummaryDto>("/api/inventory/summary", JsonDefaults.Options);

        summary!.TotalUnits.Should().Be(12);
        summary.TotalInventoryValue.Should().Be(425900m);      // sum of active list prices
        summary.AgedUnits.Should().Be(1);                      // Critical: day 128
        summary.AgedPercent.Should().Be(8.33m);                // 1 / 12
        summary.CapitalTiedInAged.Should().Be(34000m);         // cost basis of the critical active unit
        summary.AvgDaysInInventory.Should().Be(52.42m);        // mean of the active day offsets, 2dp

        summary.TierBreakdown[AgingTier.Fresh].Should().Be(5);
        summary.TierBreakdown[AgingTier.Watch].Should().Be(2);
        summary.TierBreakdown[AgingTier.Aging].Should().Be(4);
        summary.TierBreakdown[AgingTier.Critical].Should().Be(1);
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
