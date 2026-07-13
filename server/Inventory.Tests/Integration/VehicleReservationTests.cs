using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Inventory.Api.Application.Dtos;
using Inventory.Api.Domain.Enums;
using Inventory.Tests.TestSupport;

namespace Inventory.Tests.Integration;

public class VehicleReservationTests : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client;

    public VehicleReservationTests(InventoryApiFactory factory)
    {
        _client = factory.CreateAuthenticatedClientAsync().GetAwaiter().GetResult();
    }

    private async Task<VehicleListItemDto> FirstVehicle(string query = "?pageSize=1")
    {
        var page = await _client.GetFromJsonAsync<PagedResult<VehicleListItemDto>>(
            $"/api/vehicles{query}", JsonDefaults.Options);
        return page!.Items[0];
    }

    [Fact]
    public async Task Reserve_FromInStock_MarksVehicleReserved()
    {
        var vehicle = await FirstVehicle("?status=InStock&pageSize=1");

        var response = await _client.PostAsync($"/api/vehicles/{vehicle.Id}/reserve", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<VehicleDetailDto>(JsonDefaults.Options);
        updated!.Status.Should().Be(VehicleStatus.Reserved);
        updated.ClosedDate.Should().BeNull();
    }

    [Fact]
    public async Task Release_FromReserved_MarksVehicleInStock()
    {
        var vehicle = await FirstVehicle("?status=InStock&pageSize=1");
        await _client.PostAsync($"/api/vehicles/{vehicle.Id}/reserve", null);

        var response = await _client.PostAsync($"/api/vehicles/{vehicle.Id}/release", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<VehicleDetailDto>(JsonDefaults.Options);
        updated!.Status.Should().Be(VehicleStatus.InStock);
        updated.ClosedDate.Should().BeNull();
    }

    [Fact]
    public async Task Reserve_FromReserved_Returns409()
    {
        var vehicle = await FirstVehicle("?status=InStock&pageSize=1");
        await _client.PostAsync($"/api/vehicles/{vehicle.Id}/reserve", null);

        var response = await _client.PostAsync($"/api/vehicles/{vehicle.Id}/reserve", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Release_FromInStock_Returns409()
    {
        var vehicle = await FirstVehicle("?status=InStock&pageSize=1");

        var response = await _client.PostAsync($"/api/vehicles/{vehicle.Id}/release", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reserve_UnknownVehicle_Returns404()
    {
        var response = await _client.PostAsync($"/api/vehicles/{Guid.NewGuid()}/reserve", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reserve_ClosedVehicle_Returns409()
    {
        var vehicle = await FirstVehicle("?scope=Closed&pageSize=1");

        var response = await _client.PostAsync($"/api/vehicles/{vehicle.Id}/reserve", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
