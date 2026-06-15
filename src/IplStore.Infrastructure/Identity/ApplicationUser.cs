using Microsoft.AspNetCore.Identity;

namespace IplStore.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = default!;
    public string? RefreshTokenHash { get; set; }
    public DateTime? RefreshTokenExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }
    public ApplicationRole(string name) : base(name) { }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";

    public static readonly string[] All = { Admin, Customer };
}
