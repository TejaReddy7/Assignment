using IplStore.Shared;

namespace IplStore.Application.Common.Abstractions;

/// <summary>
/// Wraps ASP.NET Identity so Application handlers never touch UserManager/SignInManager directly.
/// Returns Result so failures are first-class values, not exceptions.
/// </summary>
public interface IIdentityService
{
    Task<Result<UserDescriptor>> RegisterAsync(string email, string fullName, string password, CancellationToken ct = default);
    Task<Result<UserDescriptor>> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);
    Task<Result<UserDescriptor>> FindByIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, UserDescriptor>> GetUsersByIdsAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);

    Task StoreRefreshTokenAsync(Guid userId, string refreshToken, DateTime expiresAtUtc, CancellationToken ct = default);
    Task<Result<UserDescriptor>> ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
}

public sealed record UserDescriptor(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles);
