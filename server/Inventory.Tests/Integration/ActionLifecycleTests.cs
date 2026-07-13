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

    /// <summary>Reuses an already-authenticated client (for close-vehicle tests that need an isolated factory).</summary>
    private ActionLifecycleTests(HttpClient client)
    {
        _client = client;
    }

    private async Task<Guid> FirstVehicleId()
    {
        var page = await _client.GetFromJsonAsync<PagedResult<VehicleListItemDto>>(
            "/api/vehicles?pageSize=1", JsonDefaults.Options);
        return page!.Items[0].Id;
    }

    /// <summary>Default scope returns only active (InStock/Reserved) units — the ones a deal can still close.</summary>
    private Task<Guid> FirstActiveVehicleId() => FirstVehicleId();

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

    private async Task<ActionDto> CreateAction(Guid vehicleId, ActionType type)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/vehicles/{vehicleId}/actions",
            new CreateActionRequest(type, 26000m, "Work it to close"),
            JsonDefaults.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ActionDto>(JsonDefaults.Options))!;
    }

    /// <summary>Advances an action Proposed -> Approved -> InProgress, leaving it ready to resolve.</summary>
    private async Task AdvanceToInProgress(Guid actionId)
    {
        foreach (var step in new[] { ActionStatus.Approved, ActionStatus.InProgress })
        {
            var response = await _client.PatchAsJsonAsync(
                $"/api/actions/{actionId}",
                new UpdateActionRequest(step, null),
                JsonDefaults.Options);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    private Task<VehicleDetailDto?> GetVehicle(Guid vehicleId) =>
        _client.GetFromJsonAsync<VehicleDetailDto>($"/api/vehicles/{vehicleId}", JsonDefaults.Options);

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

    [Theory]
    [InlineData(ActionType.PriceReduction, VehicleStatus.Sold)]
    [InlineData(ActionType.Transfer, VehicleStatus.Transferred)]
    [InlineData(ActionType.Auction, VehicleStatus.AtAuction)]
    public async Task ResolvingSold_ClosesTheVehicle_ViaTheActionsExitLane(ActionType type, VehicleStatus expected)
    {
        // Own factory + client: closing mutates shared inventory, so isolate from the class-fixture state.
        using var factory = new InventoryApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var lifecycle = new ActionLifecycleTests(client);

        var vehicleId = await lifecycle.FirstActiveVehicleId();
        var action = await lifecycle.CreateAction(vehicleId, type);
        await lifecycle.AdvanceToInProgress(action.Id);

        var resolve = await client.PatchAsJsonAsync(
            $"/api/actions/{action.Id}",
            new UpdateActionRequest(ActionStatus.Resolved, ActionOutcome.Sold),
            JsonDefaults.Options);
        resolve.StatusCode.Should().Be(HttpStatusCode.OK);

        var vehicle = await lifecycle.GetVehicle(vehicleId);
        vehicle!.Status.Should().Be(expected);
        vehicle.ClosedDate!.Value.Date.Should().Be(InventoryApiFactory.Now.Date);
    }

    [Fact]
    public async Task StartingPriceReduction_UpdatesVehicleListPrice()
    {
        using var factory = new InventoryApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var lifecycle = new ActionLifecycleTests(client);

        var vehicleId = await lifecycle.FirstActiveVehicleId();
        var before = await lifecycle.GetVehicle(vehicleId);
        before!.ListPrice.Should().NotBe(26000m);

        var action = await lifecycle.CreateAction(vehicleId);

        var approve = await client.PatchAsJsonAsync(
            $"/api/actions/{action.Id}",
            new UpdateActionRequest(ActionStatus.Approved, null),
            JsonDefaults.Options);
        approve.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterApproval = await lifecycle.GetVehicle(vehicleId);
        afterApproval!.ListPrice.Should().Be(before.ListPrice);

        var start = await client.PatchAsJsonAsync(
            $"/api/actions/{action.Id}",
            new UpdateActionRequest(ActionStatus.InProgress, null),
            JsonDefaults.Options);
        start.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterStart = await lifecycle.GetVehicle(vehicleId);
        afterStart!.ListPrice.Should().Be(26000m);

        var list = await client.GetFromJsonAsync<PagedResult<VehicleListItemDto>>(
            $"/api/vehicles?scope=All&search={before.Vin}", JsonDefaults.Options);
        list!.Items.Should().ContainSingle().Which.ListPrice.Should().Be(26000m);
    }

    [Fact]
    public async Task ResolvingNotSold_LeavesTheVehicleInStock()
    {
        using var factory = new InventoryApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var lifecycle = new ActionLifecycleTests(client);

        var vehicleId = await lifecycle.FirstActiveVehicleId();
        var before = await lifecycle.GetVehicle(vehicleId);
        var action = await lifecycle.CreateAction(vehicleId);
        await lifecycle.AdvanceToInProgress(action.Id);

        var resolve = await client.PatchAsJsonAsync(
            $"/api/actions/{action.Id}",
            new UpdateActionRequest(ActionStatus.Resolved, ActionOutcome.NotSold),
            JsonDefaults.Options);
        resolve.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await lifecycle.GetVehicle(vehicleId);
        after!.Status.Should().Be(before!.Status);
        after.Status.IsActive().Should().BeTrue();
        after.ClosedDate.Should().BeNull();
    }

    [Fact]
    public async Task AfterClosingViaResolve_FurtherActionsAreRejected()
    {
        using var factory = new InventoryApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var lifecycle = new ActionLifecycleTests(client);

        var vehicleId = await lifecycle.FirstActiveVehicleId();
        var action = await lifecycle.CreateAction(vehicleId);
        await lifecycle.AdvanceToInProgress(action.Id);
        await client.PatchAsJsonAsync(
            $"/api/actions/{action.Id}",
            new UpdateActionRequest(ActionStatus.Resolved, ActionOutcome.Sold),
            JsonDefaults.Options);

        // The unit is now closed: logging a fresh action against its frozen history is a 409.
        var response = await client.PostAsJsonAsync(
            $"/api/vehicles/{vehicleId}/actions",
            new CreateActionRequest(ActionType.PriceReduction, 20000m, "Too late"),
            JsonDefaults.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private record ProblemDetailsDto(string? Title, int? Status, string? Detail);
}
