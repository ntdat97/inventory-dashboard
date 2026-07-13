using Inventory.Api.Application.Auth;
using Inventory.Api.Application.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Controllers;

/// <summary>
/// Authentication surface. Real SSO is delegated to Microsoft Entra ID via OIDC; the guest/demo login mints the same
/// bearer locally so reviewers can sign in with no tenant. <c>GET /auth/me</c> echoes the current bearer's profile.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly DemoAuthOptions _demo;
    private readonly AzureAdOptions _azureAd;
    private readonly GuestTokenIssuer _tokenIssuer;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IOptions<DemoAuthOptions> demo,
        IOptions<AzureAdOptions> azureAd,
        GuestTokenIssuer tokenIssuer,
        ICurrentUserService currentUser,
        ILogger<AuthController> logger)
    {
        _demo = demo.Value;
        _azureAd = azureAd.Value;
        _tokenIssuer = tokenIssuer;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Initiates the Entra ID OIDC challenge (real SSO). Configured only when an Entra tenant is present; otherwise
    /// this returns 404 to signal SSO is not wired on this deployment (the demo uses <c>guest-login</c> instead).
    /// </summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        if (!_azureAd.IsConfigured)
        {
            return Problem(
                "Entra ID SSO is not configured on this deployment. Use the guest demo login (POST /api/auth/guest-login).",
                statusCode: StatusCodes.Status404NotFound);
        }

        var redirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUri });
    }

    /// <summary>
    /// Guest/demo login → a locally-signed JWT for the configured demo user. This is always available in the reviewer
    /// deployment so the live link is usable without a Microsoft tenant.
    /// </summary>
    [HttpPost("guest-login")]
    [AllowAnonymous]
    public ActionResult<GuestLoginResponse> GuestLogin()
    {
        var issued = _tokenIssuer.IssueForDemoUser(_demo);
        _logger.LogInformation("Guest demo login issued for {Email}.", _demo.Email);

        var profile = new UserProfileDto(_demo.Email, _demo.Email, _demo.Name, _demo.Role, _demo.DealershipId);
        return Ok(new GuestLoginResponse(issued.AccessToken, "Bearer", issued.ExpiresAtUtc, profile));
    }

    /// <summary>Current user profile from the bearer token. Requires authentication.</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserProfileDto> Me()
    {
        return Ok(new UserProfileDto(
            _currentUser.UserId,
            _currentUser.Email,
            _currentUser.Name,
            _currentUser.Role,
            _currentUser.DealershipId));
    }
}
