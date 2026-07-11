using Inventory.Api.Application.Recommendations;
using Inventory.Api.Domain.Services;
using Inventory.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Inventory.Tests.TestSupport;

/// <summary>
/// Boots the real API in-process for integration tests, swapping only the edges: an in-memory EF provider (unique
/// DB per factory), a deterministic <see cref="FakeClock"/> (so seed offsets map to exact days-in-inventory), and a
/// configurable <see cref="FakeAiClient"/> (so the LLM seam is exercised without the network). Auth runs for real
/// (JWT bearer) with a test signing key and the guest demo login enabled, so protected endpoints are exercised end-to-end.
/// </summary>
public class InventoryApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Fixed "now" for the whole app under test; seed anchors acquisition dates to this.</summary>
    public static readonly DateTime Now = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Test signing key for the demo bearer (≥ 32 bytes, no security value).</summary>
    public const string TestSigningKey = "test-only-hs256-signing-key-not-a-secret-0123456789";

    private readonly string _dbName = $"inventory-tests-{Guid.NewGuid():N}";
    private readonly IAiClient _aiClient;
    private readonly bool _demoAuthEnabled;

    /// <summary>Parameterless ctor for <c>IClassFixture</c> usage (default no-op AI fake, demo login enabled).</summary>
    public InventoryApiFactory() : this(null)
    {
    }

    internal InventoryApiFactory(IAiClient? aiClient, bool demoAuthEnabled = true)
    {
        _aiClient = aiClient ?? new FakeAiClient();
        _demoAuthEnabled = demoAuthEnabled;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // "Testing" doesn't load appsettings.Development.json, so provide the auth knobs here.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = TestSigningKey,
                ["DemoAuth:Enabled"] = _demoAuthEnabled ? "true" : "false",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the Postgres DbContext with an isolated in-memory store.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_dbName));

            // Deterministic clock so tier/days assertions are stable.
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(new FakeClock(Now));

            // Swap the real (HttpClient-backed) AI client for the test fake.
            services.RemoveAll<IAiClient>();
            services.AddSingleton(_aiClient);
        });
    }
}
