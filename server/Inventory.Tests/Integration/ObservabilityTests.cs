using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Inventory.Api.Application.Dtos;
using Inventory.Tests.TestSupport;

namespace Inventory.Tests.Integration;

/// <summary>
/// Phase 3 observability acceptance: a correlation id is assigned/echoed on the response header, unhandled errors
/// become RFC 7807 ProblemDetails without leaking a stack trace, <c>/metrics</c> exposes request/AI/business counters,
/// and <c>/health/ready</c> degrades gracefully (200 + Degraded) when the AI dependency is down.
/// </summary>
public class ObservabilityTests : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client;

    public ObservabilityTests(InventoryApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Response_CarriesGeneratedCorrelationId_WhenNoneSupplied()
    {
        var response = await _client.GetAsync("/api/inventory/summary");

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.Single().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Response_EchoesSuppliedCorrelationId()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/inventory/summary");
        request.Headers.Add("X-Correlation-Id", "test-correlation-123");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Single().Should().Be("test-correlation-123");
    }

    [Fact]
    public async Task UnhandledError_BecomesProblemDetails_WithoutStackLeak()
    {
        var response = await _client.GetAsync("/api/_diag/boom");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("InvalidOperationException");
        body.Should().NotContain("Intentional failure");
        body.Should().NotContain("at Inventory.Api"); // no stack frames
        body.Should().Contain("correlationId"); // errors are tied back to logs
    }

    [Fact]
    public async Task Metrics_ExposesRequestAiAndBusinessCounters()
    {
        // Exercise the paths that populate custom metrics: the summary sets the business gauge; the recommendation
        // endpoint records an outcome. HTTP metrics are recorded for every request by UseHttpMetrics.
        await _client.GetAsync("/api/inventory/summary");
        var page = await _client.GetFromJsonAsync<PagedResult<VehicleListItemDto>>(
            "/api/vehicles?tier=Critical&pageSize=1", JsonDefaults.Options);
        await _client.GetAsync($"/api/vehicles/{page!.Items[0].Id}/recommendation");

        var metrics = await _client.GetStringAsync("/metrics");

        metrics.Should().Contain("http_request_duration_seconds");        // request latency/count by route
        metrics.Should().Contain("inventory_recommendation_outcomes_total"); // AI/baseline outcome counter
        metrics.Should().Contain("inventory_aged_units");                 // business gauge
    }

    [Fact]
    public async Task Liveness_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_DegradesGracefully_WhenAiIsDown()
    {
        // In tests the AI provider is disabled/unconfigured, so readiness is Degraded — but still 200 (design §9).
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Degraded");
        body.Should().Contain("database");
        body.Should().Contain("ai");
    }
}
