using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Inventory.Api.Application.Dtos;
using Inventory.Api.Domain.Enums;
using Inventory.Tests.TestSupport;

namespace Inventory.Tests.Integration;

/// <summary>
/// Phase 3 auth acceptance: writes are protected (401 without a bearer, 200 with the demo bearer), and the guest
/// login mints a usable token whose profile <c>/auth/me</c> echoes.
/// </summary>
public class AuthTests
{
    private static async Task<Guid> FirstVehicleId(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<PagedResult<VehicleListItemDto>>(
            "/api/vehicles?pageSize=1", JsonDefaults.Options);
        return page!.Items[0].Id;
    }

    private static CreateActionRequest SampleAction() =>
        new(ActionType.PriceReduction, 26000m, "Cut to move it");

    [Fact]
    public async Task ProtectedWrite_WithoutToken_Returns401()
    {
        using var factory = new InventoryApiFactory();
        var client = factory.CreateClient();
        var vehicleId = await FirstVehicleId(client);

        var response = await client.PostAsJsonAsync(
            $"/api/vehicles/{vehicleId}/actions", SampleAction(), JsonDefaults.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedWrite_WithDemoToken_Succeeds()
    {
        using var factory = new InventoryApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var vehicleId = await FirstVehicleId(client);

        var response = await client.PostAsJsonAsync(
            $"/api/vehicles/{vehicleId}/actions", SampleAction(), JsonDefaults.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ProtectedPatch_WithoutToken_Returns401()
    {
        using var factory = new InventoryApiFactory();
        var client = factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            $"/api/actions/{Guid.NewGuid()}",
            new UpdateActionRequest(ActionStatus.Approved, null),
            JsonDefaults.Options);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GuestLogin_MintsToken_AndMeEchoesProfile()
    {
        using var factory = new InventoryApiFactory();
        var client = factory.CreateClient();

        var login = await client.PostAsync("/api/auth/guest-login", content: null);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<GuestLoginResponse>(JsonDefaults.Options);
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.TokenType.Should().Be("Bearer");
        body.User.Role.Should().Be("InventoryManager");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        var me = await client.GetFromJsonAsync<UserProfileDto>("/api/auth/me", JsonDefaults.Options);

        me!.Email.Should().Be(body.User.Email);
        me.Role.Should().Be("InventoryManager");
        me.Name.Should().Be(body.User.Name);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        using var factory = new InventoryApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

}
