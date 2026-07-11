using FluentAssertions;
using Inventory.Api.Application.Recommendations;
using Inventory.Api.Domain.Enums;

namespace Inventory.Tests.Application.Recommendations;

public class BaselineRecommenderTests
{
    private static readonly BaselineRecommender Recommender = new();

    private static RecommendationContext Context(
        int days, AgingTier tier, decimal listPrice = 30000m, decimal cost = 25000m, decimal carryingCost = 1000m) =>
        new(days, tier, listPrice, cost, carryingCost);

    [Fact]
    public void Fresh_RecommendsNoAction()
    {
        var result = Recommender.Recommend(Context(10, AgingTier.Fresh));

        result.Action.Should().Be(ActionType.Other);
        result.ProposedValue.Should().BeNull();
    }

    [Fact]
    public void Watch_RecommendsPromote()
    {
        var result = Recommender.Recommend(Context(45, AgingTier.Watch));

        result.Action.Should().Be(ActionType.Promote);
        result.ProposedValue.Should().BeNull();
    }

    [Fact]
    public void Aging_RecommendsFivePercentPriceReduction()
    {
        var result = Recommender.Recommend(Context(75, AgingTier.Aging, listPrice: 30000m));

        result.Action.Should().Be(ActionType.PriceReduction);
        result.ProposedValue.Should().Be(28500m); // 30000 * 0.95
    }

    [Fact]
    public void Critical_WithHealthyMargin_RecommendsTenPercentPriceReduction()
    {
        // list 30000 vs cost 24000 -> 25% margin, above the thin threshold
        var result = Recommender.Recommend(Context(120, AgingTier.Critical, listPrice: 30000m, cost: 24000m));

        result.Action.Should().Be(ActionType.PriceReduction);
        result.ProposedValue.Should().Be(27000m); // 30000 * 0.90
    }

    [Fact]
    public void Critical_WithThinMargin_RecommendsAuction()
    {
        // list 30000 vs cost 29000 -> ~3.4% margin, below the 5% thin threshold
        var result = Recommender.Recommend(Context(150, AgingTier.Critical, listPrice: 30000m, cost: 29000m));

        result.Action.Should().Be(ActionType.Auction);
        result.ProposedValue.Should().BeNull();
    }

    [Fact]
    public void Result_AlwaysCarriesGroundingFacts()
    {
        var result = Recommender.Recommend(Context(120, AgingTier.Critical));

        result.GroundingFacts.Should().NotBeEmpty();
        result.Rationale.Should().NotBeNullOrWhiteSpace();
    }
}
