using IplStore.Application.Common.Abstractions;
using IplStore.Application.Common.Messaging;
using IplStore.Domain.Entities;
using IplStore.Domain.Primitives;
using IplStore.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IplStore.Infrastructure.Persistence;

public sealed class AppDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IAppDbContext
{
    private readonly IPublisher? _publisher;

    public AppDbContext(DbContextOptions<AppDbContext> options, IPublisher? publisher = null)
        : base(options)
    {
        _publisher = publisher;
    }

    public DbSet<Franchise> Franchises => Set<Franchise>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await DispatchDomainEventsAsync(cancellationToken);
        BumpConcurrencyTokens();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Dispatches domain events BEFORE persistence so handler mutations (e.g. restock,
    /// rating recompute) are saved atomically in the same SaveChanges call. Events are
    /// cleared up-front to prevent re-dispatch if a handler queues further events.
    /// </summary>
    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        if (_publisher is null) return;

        // Drain iteratively: a handler may raise new events while mutating entities.
        while (true)
        {
            var entitiesWithEvents = ChangeTracker
                .Entries<Entity<Guid>>()
                .Where(e => e.Entity.DomainEvents.Count != 0)
                .Select(e => e.Entity)
                .ToList();

            if (entitiesWithEvents.Count == 0) break;

            var events = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();
            entitiesWithEvents.ForEach(e => e.ClearDomainEvents());

            foreach (var domainEvent in events)
                await _publisher.Publish(new DomainEventNotification(domainEvent), cancellationToken);
        }
    }

    /// <summary>
    /// Increments application-managed concurrency tokens for modified ProductVariants.
    /// EF still uses the ORIGINAL value in the UPDATE ... WHERE clause, so a competing
    /// writer that already bumped the token causes a DbUpdateConcurrencyException.
    /// Provider-agnostic (works on SQLite and SQL Server alike).
    /// </summary>
    private void BumpConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<ProductVariant>()
                     .Where(e => e.State == EntityState.Modified))
        {
            entry.Property(nameof(ProductVariant.RowVersion)).CurrentValue =
                (uint)entry.Property(nameof(ProductVariant.RowVersion)).CurrentValue! + 1;
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async ct =>
        {
            await using IDbContextTransaction transaction = await Database.BeginTransactionAsync(ct);
            try
            {
                var result = await action(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }, cancellationToken);
    }
}
