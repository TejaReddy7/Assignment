using IplStore.Domain.Primitives;
using IplStore.Domain.ValueObjects;
using IplStore.Shared;

namespace IplStore.Domain.Entities;

public sealed class CartItem : Entity<Guid>
{
    private CartItem() { } // EF

    private CartItem(Guid id, Guid cartId, Guid productId, Guid variantId,
        string productName, string? imageUrl, Money unitPrice, int quantity)
        : base(id)
    {
        CartId = cartId;
        ProductId = productId;
        ProductVariantId = variantId;
        ProductName = productName;
        ImageUrl = imageUrl;
        UnitPrice = unitPrice;
        Quantity = quantity;
        AddedAtUtc = DateTime.UtcNow;
    }

    public Guid CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid ProductVariantId { get; private set; }

    // Snapshotted for resilience against product edits while item sits in cart.
    public string ProductName { get; private set; } = default!;
    public string? ImageUrl { get; private set; }
    public Money UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public DateTime AddedAtUtc { get; private set; }

    public Money LineTotal => UnitPrice * Quantity;

    public static Result<CartItem> Create(Guid cartId, Guid productId, Guid variantId,
        string productName, string? imageUrl, Money unitPrice, int quantity)
    {
        if (quantity <= 0) return Error.Validation("cart_item.qty_invalid", "Quantity must be positive.");
        if (unitPrice.Amount <= 0) return Error.Validation("cart_item.price_invalid", "Unit price must be greater than zero.");

        return new CartItem(Guid.NewGuid(), cartId, productId, variantId, productName, imageUrl, unitPrice, quantity);
    }

    internal void SetQuantity(int qty) => Quantity = qty;
}
