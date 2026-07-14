using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.Api.Application.Auth;

/// <summary>
/// Composition-root wiring for authentication. A single JWT bearer scheme accepts the app's bearer, whether it was
/// minted by the guest/demo login (locally signed, HS256) or issued by Microsoft Entra ID (real SSO). Both are
/// validated here so downstream <c>[Authorize]</c> code is identical for the two paths (design §2 A7).
/// </summary>
public static class AuthSetup
{
    public static IServiceCollection AddInventoryAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AzureAdOptions>(configuration.GetSection(AzureAdOptions.SectionName));
        services.Configure<DemoAuthOptions>(configuration.GetSection(DemoAuthOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<GuestTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configure the bearer lazily from bound options so it reflects the finalised configuration (env, user-secrets,
        // and — in tests — layered in-memory config), rather than a snapshot read at registration time.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, IOptions<AzureAdOptions>>((bearer, jwtOptions, azureOptions) =>
            {
                var jwt = jwtOptions.Value;
                var azureAd = azureOptions.Value;

                // Real SSO: when an Entra tenant + client are configured, validate tokens against the tenant's
                // published OIDC metadata (issuer + signing keys discovered from the authority).
                if (azureAd.IsConfigured)
                {
                    bearer.Authority = azureAd.Authority;
                    bearer.Audience = azureAd.ClientId;
                }

                bearer.RequireHttpsMetadata = azureAd.IsConfigured;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromMinutes(1),

                    // The locally-signed demo bearer: accept its issuer/audience and its symmetric signing key.
                    // When Entra is also configured, its issuer/keys are merged in by the handler from the authority.
                    ValidIssuers = BuildValidIssuers(jwt, azureAd),
                    ValidAudiences = BuildValidAudiences(jwt, azureAd),
                    IssuerSigningKeys = BuildLocalSigningKeys(jwt),
                };

                // Surface *why* a bearer was rejected. Without this, every validation failure (audience/issuer
                // mismatch, expiry, wrong signing key) collapses to an opaque 401 with no server-side breadcrumb —
                // which is exactly what makes the Entra v1/v2 token mismatch hard to diagnose.
                bearer.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("Inventory.Auth.JwtBearer");
                        logger.LogWarning(
                            context.Exception,
                            "JWT bearer authentication failed: {Message}",
                            context.Exception.Message);
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();
        return services;
    }

    private static IEnumerable<string> BuildValidIssuers(JwtOptions jwt, AzureAdOptions azureAd)
    {
        var issuers = new List<string> { jwt.Issuer };
        if (azureAd.IsConfigured)
        {
            // Accept BOTH Entra token versions. Which one the tenant issues for our exposed API
            // (api://{clientId}/access_as_user) depends on the app registration's accessTokenAcceptedVersion:
            //   • v2 (accessTokenAcceptedVersion = 2) → iss = https://login.microsoftonline.com/{tenant}/v2.0
            //   • v1 (the default, null/1)          → iss = https://sts.windows.net/{tenant}/
            // Validating both means SSO works regardless of that manifest setting.
            issuers.Add($"https://login.microsoftonline.com/{azureAd.TenantId}/v2.0");
            issuers.Add($"https://sts.windows.net/{azureAd.TenantId}/");
        }

        return issuers;
    }

    private static IEnumerable<string> BuildValidAudiences(JwtOptions jwt, AzureAdOptions azureAd)
    {
        var audiences = new List<string> { jwt.Audience };
        if (azureAd.IsConfigured && !string.IsNullOrWhiteSpace(azureAd.ClientId))
        {
            // v2 access tokens carry the bare client-id GUID as the audience; v1 access tokens for a custom API
            // carry the App ID URI (api://{clientId}). Accept both so either token version validates.
            audiences.Add(azureAd.ClientId);
            audiences.Add($"api://{azureAd.ClientId}");
        }

        return audiences;
    }

    private static IEnumerable<SecurityKey> BuildLocalSigningKeys(JwtOptions jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt.SigningKey))
        {
            return Array.Empty<SecurityKey>();
        }

        return new SecurityKey[] { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)) };
    }
}
