using IplStore.Domain.Errors;
using IplStore.Domain.Primitives;
using IplStore.Domain.ValueObjects;
using IplStore.Shared;

namespace IplStore.Domain.Entities;

public sealed class ProductVariant : Entity<Guid>
{
    private ProductVariant() { } // EF

    private ProductVariant(Guid id, Guid productId, string sku, string? size, string? color, int stock, Money? priceOverride)
        : base(id)
    {
        ProductId = productId;
        Sku = sku;
        Size = size;
        Color = color;
        StockQuantity = stock;
        PriceOverride = priceOverride;
    }

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = default!;
    public string Sku { get; private set; } = default!;
    public string? Size { get; private set; }
    public string? Color { get; private set; }
    public int StockQuantity { get; private set; }
    public Money? PriceOverride { get; private set; }

    /// <summary>Concurrency token — incremented automatically by EF Core on every UPDATE.</summary>
    public uint RowVersion { get; private set; }

    public bool IsInStock => StockQuantity > 0;
    public Money EffectivePrice(Money basePrice) => PriceOverride ?? basePrice;

    public static Result<ProductVariant> Create(Guid productId, string sku, string? size, string? color, int stock, Money? priceOverride = null)
    {
        if (string.IsNullOrWhiteSpace(sku)) return Error.Validation("variant.sku_required", "SKU is required.");
        if (sku.Length > 64) return Error.Validation("variant.sku_too_long", "SKU cannot exceed 64 characters.");
        if (stock < 0) return Error.Validation("variant.stock_negative", "Stock cannot be negative.");
        if (priceOverride is { Amount: <= 0 }) return Error.Validation("variant.price_invalid", "Price override must be greater than zero.");

        return new ProductVariant(Guid.NewGuid(), productId, sku.Trim().ToUpperInvariant(), size?.Trim(), color?.Trim(), stock, priceOverride);
    }

    /// <summary>
    /// Atomically decrements stock. Returns InsufficientStock if quantity exceeds available.
    /// Optimistic concurrency is enforced by RowVersion in the EF configuration —
    /// a competing transaction will trigger a DbUpdateConcurrencyException.
    /// </summary>
    public Result Decrement(int quantity)
    {
        if (quantity <= 0) return Result.Failure(Error.Validation("variant.qty_invalid", "Quantity must be positive."));
        if (quantity > StockQuantity) return Result.Failure(DomainErrors.Variant.InsufficientStock);
        StockQuantity -= quantity;
        return Result.Success();
    }

    public void Restock(int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Restock quantity must be positive.");
        StockQuantity += quantity;
    }

    public void SetPriceOverride(Money? priceOverride) => PriceOverride = priceOverride;
}
