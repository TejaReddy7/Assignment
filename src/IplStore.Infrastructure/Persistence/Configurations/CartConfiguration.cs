using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IplStore.Infrastructure.Persistence.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");
        builder.HasKey(c => c.Id);

        builder.HasIndex(c => c.CustomerId).IsUnique();

        builder.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(i => i.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(c => c.Subtotal);
        builder.Ignore(c => c.TotalItems);
    }
}

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.ImageUrl).HasMaxLength(500);
        builder.Property(i => i.Quantity).IsRequired();

        builder.ComplexProperty(i => i.UnitPrice, money =>
        {
            money.Property(m => m.Amount).HasColumnName("UnitPriceAmount").HasColumnType("decimal(18,2)").IsRequired();
            money.Property(m => m.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(3).IsRequired();
        });

        builder.HasIndex(i => new { i.CartId, i.ProductVariantId }).IsUnique();
        builder.Ignore(i => i.LineTotal);
    }
}
