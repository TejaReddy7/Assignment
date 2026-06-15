namespace IplStore.Application.Features.Auth;

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc);

public sealed record UserProfileResponse(
    Guid UserId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles);
