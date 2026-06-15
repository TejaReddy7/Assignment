namespace IplStore.Application.Features.Cart;

public sealed record CartItemDto(
    Guid Id,
    Guid ProductId,
    Guid ProductVariantId,
    string ProductName,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record CartDto(
    Guid Id,
    IReadOnlyList<CartItemDto> Items,
    int TotalItems,
    decimal Subtotal,
    string Currency);
