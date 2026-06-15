using IplStore.Domain.Errors;
using IplStore.Domain.Primitives;
using IplStore.Domain.ValueObjects;
using IplStore.Shared;

namespace IplStore.Domain.Entities;

public sealed class Cart : Entity<Guid>, IAggregateRoot, IAuditable
{
    public const int MaxQtyPerLine = 10;
    private readonly List<CartItem> _items = new();

    private Cart() { } // EF

    private Cart(Guid id, Guid customerId)
        : base(id)
    {
        CustomerId = customerId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid CustomerId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    public Money Subtotal => _items.Count == 0
        ? Money.Zero
        : _items
            .Select(i => i.LineTotal)
            .Aggregate((a, b) => a + b);

    public int TotalItems => _items.Sum(i => i.Quantity);

    public static Cart CreateFor(Guid customerId) => new(Guid.NewGuid(), customerId);

    public Result<CartItem> AddOrMerge(
        Guid productId,
        Guid variantId,
        string productName,
        string? imageUrl,
        Money unitPrice,
        int quantity,
        int availableStock)
    {
        if (quantity <= 0) return DomainErrors.Cart.InvalidQuantity;

        var existing = _items.FirstOrDefault(i => i.ProductVariantId == variantId);
        var newQty = (existing?.Quantity ?? 0) + quantity;

        if (newQty > MaxQtyPerLine) return DomainErrors.Cart.QtyExceedsLimit;
        if (newQty > availableStock) return DomainErrors.Variant.InsufficientStock;

        if (existing is not null)
        {
            existing.SetQuantity(newQty);
            UpdatedAtUtc = DateTime.UtcNow;
            return existing;
        }

        var itemResult = CartItem.Create(Id, productId, variantId, productName, imageUrl, unitPrice, quantity);
        if (itemResult.IsFailure) return itemResult.Error;

        _items.Add(itemResult.Value);
        UpdatedAtUtc = DateTime.UtcNow;
        return itemResult.Value;
    }

    public Result UpdateQuantity(Guid itemId, int quantity, int availableStock)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return Result.Failure(DomainErrors.Cart.ItemNotFound);

        if (quantity <= 0)
        {
            _items.Remove(item);
            UpdatedAtUtc = DateTime.UtcNow;
            return Result.Success();
        }
        if (quantity > MaxQtyPerLine) return Result.Failure(DomainErrors.Cart.QtyExceedsLimit);
        if (quantity > availableStock) return Result.Failure(DomainErrors.Variant.InsufficientStock);

        item.SetQuantity(quantity);
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    public Result Remove(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return Result.Failure(DomainErrors.Cart.ItemNotFound);
        _items.Remove(item);
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    public void Clear()
    {
        _items.Clear();
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
