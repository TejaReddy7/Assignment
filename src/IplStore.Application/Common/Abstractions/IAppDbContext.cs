using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Common.Abstractions;

/// <summary>
/// Abstraction over the persistence context. Lives in Application so handlers depend on
/// an interface, not the concrete EF Core DbContext (Dependency Inversion).
/// </summary>
public interface IAppDbContext
{
    DbSet<Franchise> Franchises { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<Coupon> Coupons { get; }
    DbSet<Review> Reviews { get; }
    DbSet<WishlistItem> WishlistItems { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Executes a unit of work inside a database transaction with retry-on-concurrency.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
