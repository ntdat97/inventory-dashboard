using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Application.Recommendations;

/// <summary>
/// Pure, deterministic rule-based recommender. Always produces a result from the grounding signals — this is the
/// baseline the AI enriches but never replaces. No I/O, no clock: fully unit-tested (see design §8, principle 2).
///
/// Rules by aging tier:
///   Fresh   -> monitor (no action needed yet)
///   Watch   -> promote (lift visibility before it ages)
///   Aging   -> reduce price 5%
///   Critical-> reduce price 10%, unless margin is already thin (&lt;= 5%) -> move to auction
/// </summary>
public class BaselineRecommender
{
    private const decimal AgingReductionPct = 0.05m;
    private const decimal CriticalReductionPct = 0.10m;
    private const decimal ThinMarginThreshold = 0.05m;

    public RecommendationResult Recommend(RecommendationContext context)
    {
        var facts = BuildFacts(context);

        return context.Tier switch
        {
            AgingTier.Fresh => new RecommendationResult(
                ActionType.Other, null,
                $"Fresh stock at {context.DaysInInventory} days. No action needed yet; keep monitoring.",
                facts),

            AgingTier.Watch => new RecommendationResult(
                ActionType.Promote, null,
                $"Approaching the aging threshold at {context.DaysInInventory} days. Promote to lift visibility "
                + "before carrying cost accelerates.",
                facts),

            AgingTier.Aging => ReducePrice(context, AgingReductionPct, facts),

            _ => context.MarginRatio <= ThinMarginThreshold
                ? new RecommendationResult(
                    ActionType.Auction, null,
                    $"Critical at {context.DaysInInventory} days with a thin margin "
                    + $"({context.MarginRatio:P0}). Move to auction to stop the bleed; a further cut would sell at a loss.",
                    facts)
                : ReducePrice(context, CriticalReductionPct, facts),
        };
    }

    private static RecommendationResult ReducePrice(RecommendationContext context, decimal pct, IReadOnlyList<string> facts)
    {
        var newPrice = Math.Round(context.ListPrice * (1 - pct), 2, MidpointRounding.AwayFromZero);
        return new RecommendationResult(
            ActionType.PriceReduction,
            newPrice,
            $"{context.Tier} at {context.DaysInInventory} days, already {context.CarryingCostToDate:C0} in carrying cost. "
            + $"Recommend a {pct:P0} price cut from {context.ListPrice:C0} to {newPrice:C0} to accelerate turn.",
            facts);
    }

    private static List<string> BuildFacts(RecommendationContext context) =>
    [
        $"In stock {context.DaysInInventory} days ({context.Tier}).",
        $"List price {context.ListPrice:C0} vs cost basis {context.AcquisitionCost:C0} (margin {context.MarginRatio:P0}).",
        $"Carrying cost to date {context.CarryingCostToDate:C0}.",
    ];
}
