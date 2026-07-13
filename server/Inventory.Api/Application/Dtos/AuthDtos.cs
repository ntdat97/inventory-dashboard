namespace Inventory.Api.Application.Dtos;

/// <summary>Response for the guest/demo login: the minted bearer, its expiry, and the demo user's basic profile.</summary>
public record GuestLoginResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    UserProfileDto User);

/// <summary>The current user's profile, projected from the validated bearer's claims (design §2 A7).</summary>
public record UserProfileDto(
    string? UserId,
    string? Email,
    string? Name,
    string? Role,
    string? DealershipId);
