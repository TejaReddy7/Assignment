using DomainCart = IplStore.Domain.Entities.Cart;
using IplStore.Domain.Entities;

namespace IplStore.Application.Features.Cart;

public static class CartMappings
{
    public static CartItemDto ToDto(this CartItem item) =>
        new(
            item.Id,
            item.ProductId,
            item.ProductVariantId,
            item.ProductName,
            item.ImageUrl,
            item.UnitPrice.Amount,
            item.Quantity,
            item.LineTotal.Amount);

    public static CartDto ToDto(this DomainCart cart) =>
        new(
            cart.Id,
            cart.Items
                .OrderBy(i => i.AddedAtUtc)
                .Select(i => i.ToDto())
                .ToList(),
            cart.TotalItems,
            cart.Subtotal.Amount,
            cart.Subtotal.Currency);
}
