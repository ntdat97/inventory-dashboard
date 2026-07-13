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
        BuildAgeFact(context),
        BuildMarginFact(context),
        BuildCarryingCostFact(context),
    ];

    private static string BuildAgeFact(RecommendationContext context)
    {
        if (context.AverageActiveDaysInInventory is { } averageDays && averageDays > 0)
        {
            var delta = context.DaysInInventory - averageDays;
            var comparison = Math.Abs(delta) < 1m
                ? "in line with the active fleet average"
                : delta > 0
                    ? $"about {Math.Round(delta)} days older than the active fleet average"
                    : $"about {Math.Round(Math.Abs(delta))} days younger than the active fleet average";

            return $"{context.DaysInInventory} days in stock ({context.Tier}); {comparison}.";
        }

        return $"{context.DaysInInventory} days in stock ({context.Tier}).";
    }

    private static string BuildMarginFact(RecommendationContext context)
    {
        var marginSignal = context.MarginRatio switch
        {
            <= 0m => "underwater at current list price",
            <= ThinMarginThreshold => "thin room for discounting",
            <= 0.15m => "some discount room, but margin needs protection",
            _ => "healthy room to discount before cost basis",
        };

        return $"{context.MarginRatio:P0} margin ({context.MarginDollars:C0} above cost); {marginSignal}.";
    }

    private static string BuildCarryingCostFact(RecommendationContext context)
    {
        var dailyDrag = context.DailyCarryingCost;
        var nextThirtyDays = dailyDrag * 30m;

        if (context.AverageActiveCarryingCostToDate is { } averageCost && averageCost > 0)
        {
            var comparison = context.CarryingCostToDate >= averageCost
                ? $"above the active fleet average of {averageCost:C0}"
                : $"below the active fleet average of {averageCost:C0}";

            return $"{context.CarryingCostToDate:C0} carrying cost, {comparison}; next 30 days adds about {nextThirtyDays:C0}.";
        }

        return $"{context.CarryingCostToDate:C0} carrying cost to date; next 30 days adds about {nextThirtyDays:C0}.";
    }
}
