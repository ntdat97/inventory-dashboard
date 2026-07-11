using Inventory.Api.Domain.Enums;

namespace Inventory.Api.Application.Recommendations;

/// <summary>
/// Validates a parsed LLM recommendation before it is trusted (design §8, principle 4). Enforces a known action
/// type and sane bounds on the proposed value relative to the grounding context. Malformed output is rejected so
/// the caller falls back to the deterministic baseline.
/// </summary>
public static class AiRecommendationValidator
{
    // A price reduction shouldn't exceed 50% of list; anything beyond that is almost certainly a hallucination.
    private const decimal MaxReductionPct = 0.50m;

    public static bool IsValid(AiRecommendation candidate, RecommendationContext context)
    {
        if (!Enum.IsDefined(candidate.Action))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(candidate.Rationale))
        {
            return false;
        }

        if (candidate.ProposedValue is { } value)
        {
            if (value < 0)
            {
                return false;
            }

            // A proposed new price must be positive and not an implausibly deep cut below the current list price.
            if (candidate.Action == ActionType.PriceReduction)
            {
                var floor = context.ListPrice * (1 - MaxReductionPct);
                if (value <= 0 || value > context.ListPrice || value < floor)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
