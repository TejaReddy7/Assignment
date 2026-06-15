using System.Security.Cryptography;
using System.Text;
using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Infrastructure.Identity;
using IplStore.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<Result<UserDescriptor>> RegisterAsync(
        string email, string fullName, string password, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null) return DomainErrors.Auth.EmailTaken;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true
        };

        var create = await _userManager.CreateAsync(user, password);
        if (!create.Succeeded)
        {
            var first = create.Errors.FirstOrDefault();
            return Error.Validation("auth.register_failed", first?.Description ?? "Registration failed.");
        }

        await _userManager.AddToRoleAsync(user, Roles.Customer);
        return await DescribeAsync(user);
    }

    public async Task<Result<UserDescriptor>> ValidateCredentialsAsync(
        string email, string password, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null) return DomainErrors.Auth.InvalidCredentials;

        var valid = await _userManager.CheckPasswordAsync(user, password);
        if (!valid) return DomainErrors.Auth.InvalidCredentials;

        return await DescribeAsync(user);
    }

    public async Task<Result<UserDescriptor>> FindByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is null
            ? Error.NotFound("user.not_found", "User not found.")
            : await DescribeAsync(user);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return Array.Empty<string>();
        return (await _userManager.GetRolesAsync(user)).ToList();
    }

    public async Task StoreRefreshTokenAsync(Guid userId, string refreshToken, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return;
        user.RefreshTokenHash = Hash(refreshToken);
        user.RefreshTokenExpiresAtUtc = expiresAtUtc;
        await _userManager.UpdateAsync(user);
    }

    public async Task<Result<UserDescriptor>> ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshTokenHash == hash, ct);
        if (user is null || user.RefreshTokenExpiresAtUtc is null || user.RefreshTokenExpiresAtUtc < DateTime.UtcNow)
            return DomainErrors.Auth.InvalidRefreshToken;

        return await DescribeAsync(user);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Hash(refreshToken);
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshTokenHash == hash, ct);
        if (user is null) return;
        user.RefreshTokenHash = null;
        user.RefreshTokenExpiresAtUtc = null;
        await _userManager.UpdateAsync(user);
    }

    private async Task<UserDescriptor> DescribeAsync(ApplicationUser user)
    {
        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        return new UserDescriptor(user.Id, user.Email!, user.FullName, roles);
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(bytes);
    }
}
