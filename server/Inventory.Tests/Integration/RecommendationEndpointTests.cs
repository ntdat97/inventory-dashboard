using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Inventory.Api.Application.Dtos;
using Inventory.Api.Application.Recommendations;
using Inventory.Api.Domain.Enums;
using Inventory.Tests.TestSupport;

namespace Inventory.Tests.Integration;

public class RecommendationEndpointTests
{
    private static async Task<Guid> CriticalVehicleId(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<PagedResult<VehicleListItemDto>>(
            "/api/vehicles?tier=Critical&pageSize=1", JsonDefaults.Options);
        return page!.Items[0].Id;
    }

    [Fact]
    public async Task Recommendation_WhenAiClientThrows_ReturnsBaseline_NeverErrors()
    {
        using var factory = new InventoryApiFactory(new FakeAiClient(throws: true));
        var client = factory.CreateClient();
        var vehicleId = await CriticalVehicleId(client);

        var response = await client.GetAsync($"/api/vehicles/{vehicleId}/recommendation");

        response.StatusCode.Should().Be(HttpStatusCode.OK); // degraded, not 5xx
        var dto = await response.Content.ReadFromJsonAsync<RecommendationDto>(JsonDefaults.Options);
        dto!.Source.Should().Be(RecommendationSource.Baseline);
        dto.Rationale.Should().NotBeNullOrEmpty();
        dto.GroundingFacts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Recommendation_WhenAiSucceeds_IsEnriched()
    {
        var fake = new FakeAiClient(response: new AiRecommendation(ActionType.Promote, null, "AI-enriched rationale."));
        using var factory = new InventoryApiFactory(fake);
        var client = factory.CreateClient();
        var vehicleId = await CriticalVehicleId(client);

        var dto = await client.GetFromJsonAsync<RecommendationDto>(
            $"/api/vehicles/{vehicleId}/recommendation", JsonDefaults.Options);

        dto!.Source.Should().Be(RecommendationSource.Ai);
        dto.Rationale.Should().Be("AI-enriched rationale.");
    }

    [Fact]
    public async Task Recommendation_OnRepeat_IsServedFromCache_WithoutCallingAiAgain()
    {
        var fake = new FakeAiClient(response: new AiRecommendation(ActionType.Promote, null, "AI-enriched rationale."));
        using var factory = new InventoryApiFactory(fake);
        var client = factory.CreateClient();
        var vehicleId = await CriticalVehicleId(client);

        var first = await client.GetFromJsonAsync<RecommendationDto>(
            $"/api/vehicles/{vehicleId}/recommendation", JsonDefaults.Options);
        var second = await client.GetFromJsonAsync<RecommendationDto>(
            $"/api/vehicles/{vehicleId}/recommendation", JsonDefaults.Options);

        first!.Source.Should().Be(RecommendationSource.Ai);
        second!.Source.Should().Be(RecommendationSource.Ai);
        fake.CallCount.Should().Be(1); // second request hit the per-vehicle cache
    }

    [Fact]
    public async Task Recommendation_UnknownVehicle_Returns404()
    {
        using var factory = new InventoryApiFactory(new FakeAiClient());
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/vehicles/{Guid.NewGuid()}/recommendation");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
