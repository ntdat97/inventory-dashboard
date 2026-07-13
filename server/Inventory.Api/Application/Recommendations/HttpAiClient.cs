using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Inventory.Api.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Application.Recommendations;

/// <summary>
/// LLM client speaking the OpenAI-compatible Chat Completions protocol (Groq, OpenAI, Azure OpenAI, Ollama, … all
/// expose it). The <see cref="HttpClient"/> is wrapped by a resilience handler (timeout / retry / circuit-breaker)
/// in the composition root, so this class only shapes the request and parses the response.
///
/// Contract with the caller (<see cref="RecommendationService"/>):
///   - disabled / unconfigured  -> returns <c>null</c> (serve baseline, no network hit)
///   - unparseable / off-spec   -> returns <c>null</c> (serve baseline; validator is the second gate)
///   - transport / HTTP failure -> throws (caller catches and degrades to baseline)
/// The AI only re-words and, within bounds, refines the deterministic baseline — it never invents facts.
/// </summary>
public class HttpAiClient : IAiClient
{
    // Kept low for a decision-support tool: we want stable, grounded wording, not creative variety.
    private const double Temperature = 0.2;
    private const int MaxCompletionTokens = 400;

    // The action vocabulary the model is allowed to choose from — mirrors ActionType so the prompt can't drift.
    private static readonly string AllowedActions = string.Join(", ", Enum.GetNames<ActionType>());

    private static readonly JsonSerializerOptions ParseOptions = new(JsonSerializerDefaults.Web);

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
            temperature = Temperature,
            max_completion_tokens = MaxCompletionTokens,
            // JSON mode: the provider guarantees the message content is a single valid JSON object.
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = BuildUserPrompt(context, baseline) },
            },
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
        return ParseCompletion(body);
    }

    // ── Prompt ─────────────────────────────────────────────────────────────────────────────────────────────

    private const string SystemPrompt =
        "You are an inventory analyst for a car dealership. A deterministic rules engine has already chosen a "
        + "recommended action for an aging vehicle and gathered the hard grounding facts. Your job is to (a) keep or, only "
        + "when clearly better supported by the hard facts, adjust the recommended action, (b) write a grounded action "
        + "rationale that explains the decision without repeating the grounding bullets verbatim, and (c) add a separate "
        + "market read using the vehicle identity and broad segment/model judgment.\n"
        + "Rules:\n"
        + "- Ground the rationale in the supplied hard facts, but do not restate all numbers already shown in the bullets. Never invent numbers, dates, discounts, or dealer-specific data.\n"
        + "- Use marketRead for cautious analyst judgment about make/model/trim/age/mileage/segment demand; do not present it as measured data.\n"
        + "- Do not put unsupported market claims in grounding facts; those facts are supplied by the system only.\n"
        + "- The action MUST be exactly one of the allowed action types listed in the user message.\n"
        + "- proposedValue is a new list price in dollars for PriceReduction (a number), otherwise null.\n"
        + "- Respond with ONLY a JSON object: {\"action\": string, \"proposedValue\": number|null, \"rationale\": string, \"marketRead\": string|null}.";

    private static string BuildUserPrompt(RecommendationContext context, RecommendationResult baseline)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Vehicle identity for market judgment:");
        sb.Append("- year: ").AppendLine(context.Year == 0 ? "unknown" : context.Year.ToString());
        sb.Append("- make: ").AppendLine(string.IsNullOrWhiteSpace(context.Make) ? "unknown" : context.Make);
        sb.Append("- model: ").AppendLine(string.IsNullOrWhiteSpace(context.Model) ? "unknown" : context.Model);
        sb.Append("- trim: ").AppendLine(string.IsNullOrWhiteSpace(context.Trim) ? "unknown" : context.Trim);
        sb.Append("- mileage: ").AppendLine(context.Mileage is { } mileage ? mileage.ToString("N0") : "unknown");

        sb.AppendLine();
        sb.AppendLine("Hard grounding facts:");
        foreach (var fact in baseline.GroundingFacts)
        {
            sb.Append("- ").AppendLine(fact);
        }

        sb.AppendLine();
        sb.AppendLine("Rules-engine baseline:");
        sb.Append("- action: ").AppendLine(baseline.Action.ToString());
        sb.Append("- proposedValue: ").AppendLine(
            baseline.ProposedValue is { } v ? v.ToString("0.##") : "null");
        sb.Append("- rationale: ").AppendLine(baseline.Rationale);

        sb.AppendLine();
        sb.Append("Allowed action types: ").Append(AllowedActions).AppendLine(".");

        return sb.ToString();
    }

    // ── Response parsing ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Peels the two JSON layers of an OpenAI-style completion: the envelope
    /// (<c>choices[0].message.content</c>) and the model's own JSON payload inside that string. Returns
    /// <c>null</c> on any shape it doesn't recognise so the caller falls back to the baseline.
    /// </summary>
    private AiRecommendation? ParseCompletion(string body)
    {
        string? content;
        try
        {
            using var envelope = JsonDocument.Parse(body);
            content = envelope.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "AI response envelope was not in the expected chat-completion shape.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<AiPayload>(content, ParseOptions);
            if (parsed is null || !Enum.TryParse<ActionType>(parsed.Action, ignoreCase: true, out var action))
            {
                return null;
            }

            return new AiRecommendation(action, parsed.ProposedValue, parsed.Rationale ?? string.Empty, parsed.MarketRead);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AI message content was not valid recommendation JSON.");
            return null;
        }
    }

    /// <summary>The model's inner JSON payload, before it is mapped onto the typed <see cref="AiRecommendation"/>.</summary>
    private sealed record AiPayload(string? Action, decimal? ProposedValue, string? Rationale, string? MarketRead);
}
