using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.Api.Application.Auth;

/// <summary>
/// Mints the locally-signed (HS256) JWT bearer for the guest/demo login. This produces the <em>same</em> shape of
/// bearer the real Entra SSO flow yields, so the rest of the app treats both identically (design §2 A7).
/// </summary>
public class GuestTokenIssuer
{
    private readonly JwtOptions _jwt;

    public GuestTokenIssuer(IOptions<JwtOptions> jwt)
    {
        _jwt = jwt.Value;
    }

    /// <summary>A minted demo bearer plus the instant it expires (UTC).</summary>
    public record IssuedToken(string AccessToken, DateTime ExpiresAtUtc);

    public IssuedToken IssueForDemoUser(DemoAuthOptions demo)
    {
        if (string.IsNullOrWhiteSpace(_jwt.SigningKey) || Encoding.UTF8.GetByteCount(_jwt.SigningKey) < 32)
        {
            // Fail loudly rather than mint an insecure/unsignable token; the dev key is configured in appsettings.Development.json.
            throw new InvalidOperationException(
                "Jwt:SigningKey is missing or shorter than 32 bytes; cannot mint a demo token.");
        }

        // A bearer's validity window is real wall-clock time — it is checked by the JWT handler against the system
        // clock, independent of the domain's injected clock (which exists for deterministic aging, not token expiry).
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_jwt.TokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, demo.Email),
            new(JwtRegisteredClaimNames.Email, demo.Email),
            new("preferred_username", demo.Email),
            new(JwtRegisteredClaimNames.Name, demo.Name),
            new(ClaimTypes.Role, demo.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        if (!string.IsNullOrWhiteSpace(demo.DealershipId))
        {
            claims.Add(new Claim(AuthClaims.DealershipId, demo.DealershipId));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new IssuedToken(accessToken, expiresAt);
    }
}
