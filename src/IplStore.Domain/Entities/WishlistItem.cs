using IplStore.Domain.Primitives;

namespace IplStore.Domain.Entities;

public sealed class WishlistItem : Entity<Guid>
{
    private WishlistItem() { } // EF

    private WishlistItem(Guid id, Guid customerId, Guid productId) : base(id)
    {
        CustomerId = customerId;
        ProductId = productId;
        AddedAtUtc = DateTime.UtcNow;
    }

    public Guid CustomerId { get; private set; }
    public Guid ProductId { get; private set; }
    public DateTime AddedAtUtc { get; private set; }

    public static WishlistItem Create(Guid customerId, Guid productId)
        => new(Guid.NewGuid(), customerId, productId);
}
