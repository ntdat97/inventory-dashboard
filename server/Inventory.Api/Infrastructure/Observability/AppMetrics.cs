using Prometheus;

namespace Inventory.Api.Infrastructure.Observability;

/// <summary>
/// Application-specific Prometheus metrics beyond the automatic HTTP request count/latency (which
/// <c>UseHttpMetrics()</c> exposes). Covers the AI dependency's outcome/latency and one business signal — the
/// aged-units count served — surfaced at <c>/metrics</c> (design §9). Registered as a singleton and injected so it
/// is easy to instrument from services and to assert in tests.
/// </summary>
public class AppMetrics
{
    /// <summary>Recommendation outcomes, labelled by source so AI-vs-baseline (graceful degradation) is visible.</summary>
    private readonly Counter _recommendationOutcomes;

    /// <summary>Latency of the outbound AI enrichment call — the most useful signal for the external dependency.</summary>
    private readonly Histogram _aiCallDuration;

    /// <summary>Business gauge: number of aged (Critical) units in the most recently served summary.</summary>
    private readonly Gauge _agedUnitsServed;

    public AppMetrics(IMetricFactory? factory = null)
    {
        var metrics = factory ?? Metrics.DefaultFactory;

        _recommendationOutcomes = metrics.CreateCounter(
            "inventory_recommendation_outcomes_total",
            "Count of recommendation responses by source (ai, baseline) — degradation is visible as baseline growth.",
            new CounterConfiguration { LabelNames = new[] { "source" } });

        _aiCallDuration = metrics.CreateHistogram(
            "inventory_ai_call_duration_seconds",
            "Latency of the outbound AI enrichment call, labelled by outcome (success, failure).",
            new HistogramConfiguration { LabelNames = new[] { "outcome" } });

        _agedUnitsServed = metrics.CreateGauge(
            "inventory_aged_units",
            "Aged (Critical-tier) unit count from the most recently served inventory summary.");
    }

    public void RecordRecommendation(string source) => _recommendationOutcomes.WithLabels(source).Inc();

    public void RecordAiCall(string outcome, double durationSeconds) =>
        _aiCallDuration.WithLabels(outcome).Observe(durationSeconds);

    public void SetAgedUnits(int agedUnits) => _agedUnitsServed.Set(agedUnits);
}
