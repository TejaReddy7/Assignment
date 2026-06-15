using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IplStore.Infrastructure.Persistence.Configurations;

public sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("Coupons");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).HasMaxLength(40).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique();

        builder.Property(c => c.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Value).HasColumnType("decimal(18,2)").IsRequired();

        builder.ComplexProperty(c => c.MinOrderValue, money =>
        {
            money.IsRequired(false);
            money.Property(m => m.Amount).HasColumnName("MinOrderAmount").HasColumnType("decimal(18,2)");
            money.Property(m => m.Currency).HasColumnName("MinOrderCurrency").HasMaxLength(3);
        });

        builder.ComplexProperty(c => c.MaxDiscount, money =>
        {
            money.IsRequired(false);
            money.Property(m => m.Amount).HasColumnName("MaxDiscountAmount").HasColumnType("decimal(18,2)");
            money.Property(m => m.Currency).HasColumnName("MaxDiscountCurrency").HasMaxLength(3);
        });
    }
}

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.CustomerDisplayName).HasMaxLength(120).IsRequired();
        builder.Property(r => r.Title).HasMaxLength(120).IsRequired();
        builder.Property(r => r.Body).HasMaxLength(2000).IsRequired();
        builder.Property(r => r.Rating).IsRequired();

        // One review per (product, customer)
        builder.HasIndex(r => new { r.ProductId, r.CustomerId }).IsUnique();
    }
}

public sealed class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("WishlistItems");
        builder.HasKey(w => w.Id);
        builder.HasIndex(w => new { w.CustomerId, w.ProductId }).IsUnique();
    }
}

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Key).HasMaxLength(100).IsRequired();
        builder.Property(r => r.RequestHash).HasMaxLength(128).IsRequired();
        builder.Property(r => r.ResponseBody).IsRequired();

        builder.HasIndex(r => r.Key).IsUnique();
    }
}
