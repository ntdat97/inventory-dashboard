using System.Net.Http.Headers;
using System.Net.Http.Json;
using Inventory.Api.Application.Dtos;

namespace Inventory.Tests.TestSupport;

/// <summary>Test helpers for obtaining an authenticated client via the guest/demo login (mirrors how the SPA signs in).</summary>
public static class AuthTestExtensions
{
    /// <summary>Signs in through <c>POST /api/auth/dev-login</c> and returns the minted bearer.</summary>
    public static async Task<string> GetDemoTokenAsync(this HttpClient client)
    {
        var response = await client.PostAsync("/api/auth/dev-login", content: null);
        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<DevLoginResponse>(JsonDefaults.Options);
        return login!.AccessToken;
    }

    /// <summary>Creates a client whose default Authorization header carries a freshly minted demo bearer.</summary>
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(this InventoryApiFactory factory)
    {
        var client = factory.CreateClient();
        var token = await client.GetDemoTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
