using IplStore.Domain.Entities;
using IplStore.Domain.Enums;
using IplStore.Domain.ValueObjects;
using IplStore.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Persistence.Seeding;

public static class IdentitySeeder
{
    public const string DefaultAdminEmail = "admin@iplstore.local";
    public const string DefaultAdminPassword = "Admin#12345";
    public const string DefaultCustomerEmail = "fan@iplstore.local";
    public const string DefaultCustomerPassword = "Fan#12345";

    public static async Task SeedAsync(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }

        await EnsureUserAsync(userManager, DefaultAdminEmail, "Store Admin", DefaultAdminPassword, Roles.Admin);
        await EnsureUserAsync(userManager, DefaultCustomerEmail, "IPL Fan", DefaultCustomerPassword, Roles.Customer);
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager, string email, string fullName, string password, string role)
    {
        if (await userManager.FindByEmailAsync(email) is not null) return;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, role);
    }
}

public static class CouponSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Coupons.AnyAsync(ct)) return;

        var welcome = Coupon.Create("WELCOME10", CouponType.Percentage, 10m,
            Money.From(999m), Money.From(500m), DateTime.UtcNow.AddYears(1), 10_000).Value;

        var flat200 = Coupon.Create("IPL200", CouponType.FixedAmount, 200m,
            Money.From(1500m), null, DateTime.UtcNow.AddMonths(6), 5_000).Value;

        db.Coupons.AddRange(welcome, flat200);
        await db.SaveChangesAsync(ct);
    }
}
