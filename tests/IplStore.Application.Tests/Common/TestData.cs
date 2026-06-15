using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Entities;
using IplStore.Domain.Enums;
using IplStore.Domain.ValueObjects;
using IplStore.Infrastructure.Persistence;

namespace IplStore.Application.Tests.Common;

/// <summary>Builds small, deterministic catalog fixtures for handler tests.</summary>
public static class TestData
{
    public static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task<(Product Product, ProductVariant Variant)> SeedProductAsync(
        AppDbContext db, int stock = 50, decimal price = 1999m)
    {
        var franchise = Franchise.Create("Mumbai Indians", "MI", "Mumbai", "#004BA0", 2008).Value;
        db.Franchises.Add(franchise);

        var product = Product.Create("MI Home Jersey", "Official MI home jersey.",
            ProductType.Jersey, franchise.Id, Money.From(price), null).Value;
        var variant = product.AddVariant("MI-JSY-M", "M", "Blue", stock).Value;
        db.Products.Add(product);

        await db.SaveChangesAsync();
        return (product, variant);
    }

    public static async Task<Coupon> SeedCouponAsync(AppDbContext db, string code = "SAVE10", decimal percent = 10)
    {
        var coupon = Coupon.Create(code, CouponType.Percentage, percent, null, null,
            DateTime.UtcNow.AddDays(30), 1000).Value;
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        return coupon;
    }
}

/// <summary>Test double for ICurrentUser returning a fixed identity.</summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser(Guid? userId, bool isAdmin = false)
    {
        UserId = userId;
        IsAdmin = isAdmin;
    }

    public Guid? UserId { get; }
    public string? Email => "test@iplstore.local";
    public bool IsAuthenticated => UserId is not null;
    public bool IsAdmin { get; }
    public bool IsInRole(string role) => IsAdmin && role == "Admin";
}

/// <summary>No-op cache used in handler tests.</summary>
public sealed class NoOpCache : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>Deterministic order-number generator for tests.</summary>
public sealed class FakeOrderNumberGenerator : IOrderNumberGenerator
{
    private int _counter;
    public string Next() => $"ORD-TEST-{++_counter:D4}";
}

/// <summary>Configurable payment gateway double.</summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly bool _succeeds;
    public FakePaymentGateway(bool succeeds = true) => _succeeds = succeeds;

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default)
        => Task.FromResult(_succeeds
            ? PaymentResult.Ok($"txn_{Guid.NewGuid():N}")
            : PaymentResult.Declined("Card declined."));
}
