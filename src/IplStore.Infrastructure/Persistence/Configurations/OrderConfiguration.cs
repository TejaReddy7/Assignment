using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IplStore.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(o => o.OrderNumber).IsUnique();

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.PaymentTransactionId).HasMaxLength(100);
        builder.Property(o => o.CouponCode).HasMaxLength(40);
        builder.Property(o => o.CancellationReason).HasMaxLength(500);

        // Idempotency: a given (customer, key) maps to exactly one order.
        builder.Property(o => o.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(o => new { o.CustomerId, o.IdempotencyKey }).IsUnique();
        builder.HasIndex(o => new { o.CustomerId, o.PlacedAtUtc });

        // Shipping address as owned value object
        builder.OwnsOne(o => o.ShippingAddress, addr =>
        {
            addr.Property(a => a.Line1).HasColumnName("ShipLine1").HasMaxLength(200).IsRequired();
            addr.Property(a => a.Line2).HasColumnName("ShipLine2").HasMaxLength(200);
            addr.Property(a => a.City).HasColumnName("ShipCity").HasMaxLength(80).IsRequired();
            addr.Property(a => a.State).HasColumnName("ShipState").HasMaxLength(80).IsRequired();
            addr.Property(a => a.PostalCode).HasColumnName("ShipPostalCode").HasMaxLength(20).IsRequired();
            addr.Property(a => a.Country).HasColumnName("ShipCountry").HasMaxLength(80).IsRequired();
        });

        OwnMoney(builder, o => o.Subtotal, "Subtotal");
        OwnMoney(builder, o => o.DiscountAmount, "Discount");
        OwnMoney(builder, o => o.ShippingFee, "Shipping");
        OwnMoney(builder, o => o.Total, "Total");

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void OwnMoney(
        EntityTypeBuilder<Order> builder,
        System.Linq.Expressions.Expression<Func<Order, Domain.ValueObjects.Money>> nav,
        string prefix)
    {
        builder.ComplexProperty(nav, money =>
        {
            money.Property(m => m.Amount).HasColumnName($"{prefix}Amount").HasColumnType("decimal(18,2)").IsRequired();
            money.Property(m => m.Currency).HasColumnName($"{prefix}Currency").HasMaxLength(3).IsRequired();
        });
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(i => i.SkuSnapshot).HasMaxLength(64).IsRequired();
        builder.Property(i => i.Quantity).IsRequired();

        builder.ComplexProperty(i => i.UnitPrice, money =>
        {
            money.Property(m => m.Amount).HasColumnName("UnitPriceAmount").HasColumnType("decimal(18,2)").IsRequired();
            money.Property(m => m.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(3).IsRequired();
        });

        builder.Ignore(i => i.LineTotal);
    }
}
