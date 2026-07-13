using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Inventory.Api.Application.Dtos;
using Inventory.Api.Domain.Enums;
using Inventory.Tests.TestSupport;

namespace Inventory.Tests.Integration;

/// <summary>
/// A closed vehicle (Sold/Transferred/AtAuction) has left the risk ledger. Its derived metrics are frozen at ClosedDate,
/// its action history is read-only (writes -> 409), and no recommendation applies (-> 409, spending no AI call). These
/// tests pin that contract against the seed's closed units.
/// </summary>
public class ClosedVehicleTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public ClosedVehicleTests(InventoryApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<VehicleListItemDto> FirstClosedVehicle(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<PagedResult<VehicleListItemDto>>(
            "/api/vehicles?scope=Closed&pageSize=100", JsonDefaults.Options);
        return page!.Items[0];
    }

    [Fact]
    public async Task ClosedVehicle_ExposesClosedDate_AndFrozenDaysInInventory()
    {
        var client = _factory.CreateClient();

        var page = await client.GetFromJsonAsync<PagedResult<VehicleListItemDto>>(
            "/api/vehicles?scope=Closed&pageSize=100", JsonDefaults.Options);

        page!.Items.Should().OnlyContain(v => v.ClosedDate != null);

        // days-in-inventory is frozen at ClosedDate: (ClosedDate - AcquisitionDate), not (now - AcquisitionDate).
        foreach (var v in page.Items)
        {
            var expectedDays = (int)(v.ClosedDate!.Value.Date - v.AcquisitionDate.Date).TotalDays;
            v.DaysInInventory.Should().Be(expectedDays);
        }
    }

    [Fact]
    public async Task Recommendation_ForClosedVehicle_Returns409_NotAiCall()
    {
        var client = _factory.CreateClient();
        var closed = await FirstClosedVehicle(client);

        var response = await client.GetAsync($"/api/vehicles/{closed.Id}/recommendation");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task CreateAction_OnClosedVehicle_Returns409()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var closed = await FirstClosedVehicle(client);

        var response = await client.PostAsJsonAsync(
            $"/api/vehicles/{closed.Id}/actions",
            new CreateActionRequest(ActionType.PriceReduction, 20000m, "Should be rejected"),
            JsonDefaults.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TransitionAction_OnClosedVehicle_Returns409()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        // The seed logs a resolved PriceReduction on each sold unit; find one via the detail history.
        var closedPage = await client.GetFromJsonAsync<PagedResult<VehicleListItemDto>>(
            "/api/vehicles?scope=Closed&pageSize=100", JsonDefaults.Options);

        ActionDto? seededAction = null;
        foreach (var v in closedPage!.Items)
        {
            var detail = await client.GetFromJsonAsync<VehicleDetailDto>(
                $"/api/vehicles/{v.Id}", JsonDefaults.Options);
            seededAction = detail!.History.FirstOrDefault();
            if (seededAction is not null)
            {
                break;
            }
        }

        seededAction.Should().NotBeNull("the seed logs a resolved action on closed sold units");

        var response = await client.PatchAsJsonAsync(
            $"/api/actions/{seededAction!.Id}",
            new UpdateActionRequest(ActionStatus.Approved, null),
            JsonDefaults.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
