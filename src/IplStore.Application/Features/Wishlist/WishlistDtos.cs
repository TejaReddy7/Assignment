using IplStore.Application.Features.Catalog;

namespace IplStore.Application.Features.Wishlist;

public sealed record WishlistItemDto(
    Guid ProductId,
    string Name,
    string Slug,
    decimal Price,
    string Currency,
    string? ImageUrl,
    string FranchiseShortCode,
    bool InStock,
    DateTime AddedAtUtc);
