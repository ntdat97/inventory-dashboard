using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Inventory.Api.Application.Dtos;
using Inventory.Api.Domain.Enums;
using Inventory.Tests.TestSupport;

namespace Inventory.Tests.Integration;

public class VehiclesEndpointsTests : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client;

    public VehiclesEndpointsTests(InventoryApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private Task<PagedResult<VehicleListItemDto>?> GetPage(string query) =>
        _client.GetFromJsonAsync<PagedResult<VehicleListItemDto>>($"/api/vehicles{query}", JsonDefaults.Options);

    [Fact]
    public async Task List_WithoutFilters_ReturnsAllSeededVehicles()
    {
        var page = await GetPage("?pageSize=100");

        page!.TotalCount.Should().Be(12);
        page.Items.Should().HaveCount(12);
    }

    [Fact]
    public async Task List_FilterByTierCritical_ReturnsOnlyCriticalUnits()
    {
        var page = await GetPage("?tier=Critical&pageSize=100");

        page!.Items.Should().HaveCount(3);
        page.Items.Should().OnlyContain(v => v.Tier == AgingTier.Critical);
    }

    [Fact]
    public async Task List_FilterByMake_ReturnsMatchingVehicle()
    {
        var page = await GetPage("?make=Toyota");

        page!.Items.Should().ContainSingle();
        page.Items[0].Make.Should().Be("Toyota");
        page.Items[0].Model.Should().Be("Camry");
    }

    [Fact]
    public async Task List_Pagination_ReturnsRequestedSliceAndTotals()
    {
        var first = await GetPage("?page=1&pageSize=5");
        first!.Items.Should().HaveCount(5);
        first.TotalCount.Should().Be(12);
        first.TotalPages.Should().Be(3);

        var last = await GetPage("?page=3&pageSize=5");
        last!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task List_DefaultSort_IsMostAgedFirst()
    {
        var page = await GetPage("?pageSize=100");

        page!.Items.First().DaysInInventory.Should().Be(210); // the oldest seeded unit
        page.Items.Should().BeInDescendingOrder(v => v.DaysInInventory);
    }

    [Fact]
    public async Task List_FilterByDayRange_UsesDerivedDaysInInventory()
    {
        var page = await GetPage("?minDays=61&maxDays=90&pageSize=100");

        page!.Items.Should().OnlyContain(v => v.DaysInInventory >= 61 && v.DaysInInventory <= 90);
        page.Items.Should().OnlyContain(v => v.Tier == AgingTier.Aging);
    }

    [Fact]
    public async Task GetById_ReturnsVehicleWithDerivedFields()
    {
        var list = await GetPage("?pageSize=1");
        var id = list!.Items[0].Id;

        var detail = await _client.GetFromJsonAsync<VehicleDetailDto>($"/api/vehicles/{id}", JsonDefaults.Options);

        detail!.Id.Should().Be(id);
        detail.DealershipName.Should().NotBeNullOrEmpty();
        detail.History.Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/vehicles/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Aging_ReturnsAgingAndCriticalSubset()
    {
        var vehicles = await _client.GetFromJsonAsync<List<VehicleListItemDto>>("/api/inventory/aging", JsonDefaults.Options);

        vehicles!.Should().OnlyContain(v => v.Tier == AgingTier.Aging || v.Tier == AgingTier.Critical);
        vehicles.Should().HaveCount(6); // 3 Aging + 3 Critical in the seed
    }
}
