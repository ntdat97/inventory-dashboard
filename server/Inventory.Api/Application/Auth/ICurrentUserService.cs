using System.Security.Claims;

namespace Inventory.Api.Application.Auth;

/// <summary>
/// The current user's identity, resolved from the validated bearer token. A seam over <see cref="HttpContext"/>
/// so services depend on an interface (not the framework) and it can be faked in tests. Both the Entra SSO path and
/// the demo login populate the same claims, so callers treat them identically (design §2 A7).
/// </summary>
public interface ICurrentUserService
{
    /// <summary>True when the request carries a valid, authenticated bearer.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Stable subject id from the token (the <c>sub</c>/NameIdentifier claim), or null when anonymous.</summary>
    string? UserId { get; }

    /// <summary>The user's email / preferred username, or null when anonymous.</summary>
    string? Email { get; }

    /// <summary>The user's display name, or null when anonymous.</summary>
    string? Name { get; }

    /// <summary>The user's role claim, or null when anonymous.</summary>
    string? Role { get; }

    /// <summary>The dealership the user is scoped to, if the token carries one.</summary>
    string? DealershipId { get; }
}

/// <summary>Reads the current user's claims from the ambient <see cref="HttpContext"/> populated by JWT bearer auth.</summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? UserId =>
        Find(ClaimTypes.NameIdentifier) ?? Find("sub");

    public string? Email =>
        Find(ClaimTypes.Email) ?? Find("email") ?? Find("preferred_username");

    public string? Name =>
        Find(ClaimTypes.Name) ?? Find("name");

    public string? Role =>
        Find(ClaimTypes.Role) ?? Find("role");

    public string? DealershipId => Find(AuthClaims.DealershipId);

    private string? Find(string type)
    {
        var value = Principal?.FindFirst(type)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
