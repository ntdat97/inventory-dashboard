using Inventory.Api.Application.Recommendations;

namespace Inventory.Tests.TestSupport;

/// <summary>
/// Configurable fake <see cref="IAiClient"/> for integration tests. It can be told to throw (to exercise graceful
/// degradation) or to return a canned enrichment, and it counts invocations (to prove cache hits skip the LLM).
/// </summary>
public class FakeAiClient : IAiClient
{
    private readonly bool _throws;
    private readonly AiRecommendation? _response;

    public FakeAiClient(bool throws = false, AiRecommendation? response = null)
    {
        _throws = throws;
        _response = response;
    }

    public int CallCount { get; private set; }
    public RecommendationContext? LastContext { get; private set; }

    public Task<AiRecommendation?> EnrichAsync(
        RecommendationContext context, RecommendationResult baseline, CancellationToken ct)
    {
        CallCount++;
        LastContext = context;
        if (_throws)
        {
            throw new InvalidOperationException("Simulated AI provider failure.");
        }

        return Task.FromResult(_response);
    }
}
