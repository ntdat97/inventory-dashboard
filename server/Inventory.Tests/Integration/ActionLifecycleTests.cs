using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Inventory.Api.Application.Dtos;
using Inventory.Api.Domain.Enums;
using Inventory.Tests.TestSupport;

namespace Inventory.Tests.Integration;

public class ActionLifecycleTests : IClassFixture<InventoryApiFactory>
{
    private readonly HttpClient _client;

    public ActionLifecycleTests(InventoryApiFactory factory)
    {
        // Writes require authentication; sign in via the guest demo login so the lifecycle is exercised end-to-end.
        _client = factory.CreateAuthenticatedClientAsync().GetAwaiter().GetResult();
    }

    private async Task<Guid> FirstVehicleId()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<VehicleListItemDto>>(
            "/api/vehicles?pageSize=1", JsonDefaults.Options);
        return page!.Items[0].Id;
    }

    private async Task<ActionDto> CreateAction(Guid vehicleId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/vehicles/{vehicleId}/actions",
            new CreateActionRequest(ActionType.PriceReduction, 26000m, "Cut to move it"),
            JsonDefaults.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var action = await response.Content.ReadFromJsonAsync<ActionDto>(JsonDefaults.Options);
        return action!;
    }

    [Fact]
    public async Task CreateAction_StartsAsProposed_AndAppearsInHistory()
    {
        var vehicleId = await FirstVehicleId();

        var action = await CreateAction(vehicleId);

        action.Status.Should().Be(ActionStatus.Proposed);
        action.Type.Should().Be(ActionType.PriceReduction);

        var detail = await _client.GetFromJsonAsync<VehicleDetailDto>(
            $"/api/vehicles/{vehicleId}", JsonDefaults.Options);
        detail!.History.Should().ContainSingle(a => a.Id == action.Id);
    }

    [Fact]
    public async Task ValidTransition_ProposedToApproved_Succeeds()
    {
        var vehicleId = await FirstVehicleId();
        var action = await CreateAction(vehicleId);

        var response = await _client.PatchAsJsonAsync(
            $"/api/actions/{action.Id}",
            new UpdateActionRequest(ActionStatus.Approved, null),
            JsonDefaults.Options);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<ActionDto>(JsonDefaults.Options);
        updated!.Status.Should().Be(ActionStatus.Approved);
    }

    [Fact]
    public async Task InvalidTransition_ProposedToResolved_Returns409ProblemDetails()
    {
        var vehicleId = await FirstVehicleId();
        var action = await CreateAction(vehicleId);

        var response = await _client.PatchAsJsonAsync(
            $"/api/actions/{action.Id}",
            new UpdateActionRequest(ActionStatus.Resolved, ActionOutcome.Sold),
            JsonDefaults.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonDefaults.Options);
        problem!.Status.Should().Be(409);
        problem.Detail.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Transition_UnknownAction_Returns404()
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/actions/{Guid.NewGuid()}",
            new UpdateActionRequest(ActionStatus.Approved, null),
            JsonDefaults.Options);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private record ProblemDetailsDto(string? Title, int? Status, string? Detail);
}
