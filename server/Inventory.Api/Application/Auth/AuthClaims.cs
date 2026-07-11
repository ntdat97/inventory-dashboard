namespace Inventory.Api.Application.Auth;

/// <summary>Custom claim types shared between the token issuer and the current-user reader.</summary>
public static class AuthClaims
{
    /// <summary>The dealership a user is scoped to. Not a standard JWT claim, hence named explicitly.</summary>
    public const string DealershipId = "dealershipId";
}
