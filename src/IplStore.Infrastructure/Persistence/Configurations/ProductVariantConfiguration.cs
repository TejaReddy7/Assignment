using IplStore.Domain.Entities;
using IplStore.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IplStore.Infrastructure.Persistence.Configurations;

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Sku).HasMaxLength(64).IsRequired();
        builder.Property(v => v.Size).HasMaxLength(20);
        builder.Property(v => v.Color).HasMaxLength(40);
        builder.Property(v => v.StockQuantity).IsRequired();

        // Application-managed optimistic concurrency token. We deliberately avoid
        // IsRowVersion() because that requires a DB-generated value (SQL Server rowversion),
        // which SQLite lacks. Instead AppDbContext increments this on every modify, and EF
        // includes the original value in the UPDATE ... WHERE clause across all providers.
        builder.Property(v => v.RowVersion).IsConcurrencyToken().ValueGeneratedNever();

        builder.ComplexProperty(v => v.PriceOverride, money =>
        {
            money.IsRequired(false);
            money.Property(m => m.Amount).HasColumnName("PriceOverrideAmount").HasColumnType("decimal(18,2)");
            money.Property(m => m.Currency).HasColumnName("PriceOverrideCurrency").HasMaxLength(3);
        });

        builder.HasIndex(v => v.Sku).IsUnique();
    }
}
