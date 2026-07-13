namespace Inventory.Api.Application.Auth;

/// <summary>
/// Signing/validation parameters for the JWT bearer the app accepts. In demo mode the token is locally signed with
/// <see cref="SigningKey"/> (HS256); the same issuer/audience are asserted on validation. The real Entra flow
/// (see <see cref="AzureAdOptions"/>) mints an equivalent bearer, so downstream code treats both identically (design §2 A7).
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Token issuer asserted on both mint and validation for the locally-signed demo bearer.</summary>
    public string Issuer { get; set; } = "inventory-api";

    /// <summary>Audience asserted on both mint and validation for the locally-signed demo bearer.</summary>
    public string Audience { get; set; } = "inventory-client";

    /// <summary>
    /// Symmetric signing key for the demo bearer. Dev-only, labelled, no security value — the real value lives in
    /// <c>appsettings.Development.json</c> / env, never a production secret. Must be ≥ 32 bytes for HS256.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Lifetime of a minted demo token.</summary>
    public int TokenLifetimeMinutes { get; set; } = 480;
}

/// <summary>
/// Microsoft Entra ID (Azure AD) OIDC configuration. When <see cref="ClientId"/> and <see cref="TenantId"/> are
/// present the JWT bearer additionally validates Entra-issued tokens against the tenant's published metadata — the
/// real SSO path. Left empty for the zero-setup demo, where only the locally-signed bearer is accepted.
/// </summary>
public class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    /// <summary>Entra login authority host.</summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>Directory (tenant) id.</summary>
    public string? TenantId { get; set; }

    /// <summary>Application (client) id — also the expected token audience.</summary>
    public string? ClientId { get; set; }

    /// <summary>True only when the tenant + client are configured (real SSO wired).</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId);

    /// <summary>The v2.0 authority URL used to discover Entra's signing keys/issuer.</summary>
    public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}/v2.0";
}

/// <summary>
/// Guest/demo login profile. The demo path is intentionally always available for this reviewer deployment; real Entra
/// SSO remains present as the production-style path, but reviewers are not expected to have a tenant account.
/// </summary>
public class DemoAuthOptions
{
    public const string SectionName = "DemoAuth";

    /// <summary>Demo user's email (becomes the token's <c>email</c> / <c>preferred_username</c> claim).</summary>
    public string Email { get; set; } = "demo.manager@datnguyen-demo.com";

    /// <summary>Demo user's display name.</summary>
    public string Name { get; set; } = "Demo Manager";

    /// <summary>Demo user's role claim.</summary>
    public string Role { get; set; } = "InventoryManager";

    /// <summary>Optional dealership the demo user is scoped to (flows into a <c>dealershipId</c> claim).</summary>
    public string? DealershipId { get; set; }
}
