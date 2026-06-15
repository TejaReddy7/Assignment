using IplStore.Domain.Primitives;
using IplStore.Domain.ValueObjects;
using IplStore.Shared;

namespace IplStore.Domain.Entities;

public sealed class OrderItem : Entity<Guid>
{
    private OrderItem() { } // EF

    private OrderItem(Guid id, Guid orderId, Guid productId, Guid variantId,
        string productSnapshot, string skuSnapshot, Money unitPrice, int quantity)
        : base(id)
    {
        OrderId = orderId;
        ProductId = productId;
        ProductVariantId = variantId;
        ProductSnapshot = productSnapshot;
        SkuSnapshot = skuSnapshot;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid ProductVariantId { get; private set; }

    // Snapshots — preserve historical truth even if the product is renamed/deleted later.
    public string ProductSnapshot { get; private set; } = default!;
    public string SkuSnapshot { get; private set; } = default!;
    public Money UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public Money LineTotal => UnitPrice * Quantity;

    public static Result<OrderItem> Create(Guid productId, Guid variantId,
        string productSnapshot, string skuSnapshot, Money unitPrice, int quantity)
    {
        if (quantity <= 0) return Error.Validation("order_item.qty_invalid", "Quantity must be positive.");
        if (unitPrice.Amount <= 0) return Error.Validation("order_item.price_invalid", "Unit price must be greater than zero.");

        return new OrderItem(Guid.NewGuid(), Guid.Empty, productId, variantId,
            productSnapshot, skuSnapshot, unitPrice, quantity);
    }
}
