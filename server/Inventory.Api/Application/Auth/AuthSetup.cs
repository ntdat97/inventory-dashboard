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
        services.AddScoped<DevTokenIssuer>();

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
            });

        services.AddAuthorization();
        return services;
    }

    private static IEnumerable<string> BuildValidIssuers(JwtOptions jwt, AzureAdOptions azureAd)
    {
        var issuers = new List<string> { jwt.Issuer };
        if (azureAd.IsConfigured)
        {
            issuers.Add($"https://login.microsoftonline.com/{azureAd.TenantId}/v2.0");
        }

        return issuers;
    }

    private static IEnumerable<string> BuildValidAudiences(JwtOptions jwt, AzureAdOptions azureAd)
    {
        var audiences = new List<string> { jwt.Audience };
        if (azureAd.IsConfigured && !string.IsNullOrWhiteSpace(azureAd.ClientId))
        {
            audiences.Add(azureAd.ClientId);
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
