using IplStore.Domain.Entities;
using IplStore.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IplStore.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(4000).IsRequired();
        builder.Property(p => p.ImageUrl).HasMaxLength(500);
        builder.Property(p => p.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(p => p.AverageRating).HasColumnType("decimal(3,2)");

        // Slug value object -> string column
        builder.Property(p => p.Slug)
            .HasConversion(s => s.Value, v => Slug.Create(v).Value)
            .HasMaxLength(200)
            .IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();

        // Money value object -> complex type columns (struct VO requires ComplexProperty, not OwnsOne)
        builder.ComplexProperty(p => p.BasePrice, money =>
        {
            money.Property(m => m.Amount).HasColumnName("BasePriceAmount").HasColumnType("decimal(18,2)").IsRequired();
            money.Property(m => m.Currency).HasColumnName("BasePriceCurrency").HasMaxLength(3).IsRequired();
        });

        builder.HasMany(p => p.Variants)
            .WithOne(v => v.Product)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Reviews)
            .WithOne()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Search & filtering indexes
        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.Type);
        builder.HasIndex(p => new { p.FranchiseId, p.Type });
        builder.HasIndex(p => p.IsActive);

        // Soft-delete global filter
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
