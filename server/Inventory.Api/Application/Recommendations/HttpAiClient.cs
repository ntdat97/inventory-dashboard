using System.Net.Http.Json;
using System.Text.Json;
using Inventory.Api.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Application.Recommendations;

/// <summary>
/// Provider-agnostic LLM client. The <see cref="HttpClient"/> is configured with a resilience handler
/// (timeout / retry / circuit-breaker) in the composition root, so this class only shapes the request and parses
/// the response. When the feature is disabled or unconfigured it returns <c>null</c> (caller serves baseline);
/// on a genuine call/parse failure it throws so the caller can degrade gracefully.
/// </summary>
public class HttpAiClient : IAiClient
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly ILogger<HttpAiClient> _logger;

    public HttpAiClient(HttpClient http, IOptions<AiOptions> options, ILogger<HttpAiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AiRecommendation?> EnrichAsync(
        RecommendationContext context, RecommendationResult baseline, CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            // Unconfigured provider: skip the network entirely and let the caller serve the baseline.
            return null;
        }

        var payload = new
        {
            model = _options.Model,
            grounding = baseline.GroundingFacts,
            baseline = new { action = baseline.Action.ToString(), proposedValue = baseline.ProposedValue },
            instruction = "Return JSON {action, proposedValue, rationale}. Keep the action within the allowed set "
                + "and stay grounded in the supplied facts.",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = JsonContent.Create(payload),
        };
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.ApiKey}");
        }

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        return Parse(body);
    }

    private static AiRecommendation? Parse(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("action", out var actionEl)
            || !Enum.TryParse<ActionType>(actionEl.GetString(), ignoreCase: true, out var action))
        {
            return null;
        }

        var rationale = root.TryGetProperty("rationale", out var rEl) ? rEl.GetString() ?? string.Empty : string.Empty;
        decimal? proposedValue = root.TryGetProperty("proposedValue", out var pEl)
            && pEl.ValueKind is JsonValueKind.Number
            ? pEl.GetDecimal()
            : null;

        return new AiRecommendation(action, proposedValue, rationale);
    }
}
