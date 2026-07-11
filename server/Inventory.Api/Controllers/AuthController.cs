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
/// The demo path is gated by the explicit <c>DemoAuth:Enabled</c> flag (design §2 A7).
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly DemoAuthOptions _demo;
    private readonly AzureAdOptions _azureAd;
    private readonly DevTokenIssuer _tokenIssuer;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IOptions<DemoAuthOptions> demo,
        IOptions<AzureAdOptions> azureAd,
        DevTokenIssuer tokenIssuer,
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
    /// this returns 404 to signal SSO is not wired on this deployment (the demo uses <c>dev-login</c> instead).
    /// </summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        if (!_azureAd.IsConfigured)
        {
            return Problem(
                "Entra ID SSO is not configured on this deployment. Use the guest demo login (POST /api/auth/dev-login).",
                statusCode: StatusCodes.Status404NotFound);
        }

        var redirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUri });
    }

    /// <summary>
    /// Guest/demo login → a locally-signed JWT for the configured demo user. Gated by <c>DemoAuth:Enabled</c>:
    /// when the flag is off this returns 404 (the endpoint effectively does not exist), leaving SSO the only path.
    /// </summary>
    [HttpPost("dev-login")]
    [AllowAnonymous]
    public ActionResult<DevLoginResponse> DevLogin()
    {
        if (!_demo.Enabled)
        {
            return Problem(
                "Guest demo login is disabled (DemoAuth:Enabled=false).",
                statusCode: StatusCodes.Status404NotFound);
        }

        var issued = _tokenIssuer.IssueForDemoUser(_demo);
        _logger.LogInformation("Guest demo login issued for {Email}.", _demo.Email);

        var profile = new UserProfileDto(_demo.Email, _demo.Email, _demo.Name, _demo.Role, _demo.DealershipId);
        return Ok(new DevLoginResponse(issued.AccessToken, "Bearer", issued.ExpiresAtUtc, profile));
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
