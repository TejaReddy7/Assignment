using DomainCart = IplStore.Domain.Entities.Cart;
using IplStore.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Cart;

/// <summary>
/// Loads or creates the current customer's cart (with items) inside the given context.
/// Centralizes the get-or-create logic shared by all cart command handlers.
/// </summary>
public static class CartLoader
{
    public static async Task<DomainCart> GetOrCreateAsync(
        IAppDbContext db, Guid customerId, CancellationToken ct)
    {
        var cart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

        if (cart is not null) return cart;

        cart = DomainCart.CreateFor(customerId);
        db.Carts.Add(cart);
        return cart;
    }
}
