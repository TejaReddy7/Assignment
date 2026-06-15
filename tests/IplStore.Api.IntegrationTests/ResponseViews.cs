namespace IplStore.Api.IntegrationTests;

// Lightweight view models for deserializing API responses in tests.
// Kept separate from the production DTOs so tests assert on the wire contract.

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record MoneyView(decimal Amount, string Currency);

public sealed record ProductListItemView(
    Guid Id,
    string Name,
    string Slug,
    MoneyView BasePrice,
    string FranchiseShortCode,
    bool InStock);

public sealed record ProductDetailsView(
    Guid Id,
    string Name,
    string Slug,
    IReadOnlyList<VariantView> Variants);

public sealed record VariantView(Guid Id, string Sku, int StockQuantity, bool InStock);

public sealed record FranchiseView(Guid Id, string Name, string ShortCode);

public sealed record FacetCountView(string Value, string Label, int Count);

public sealed record SearchFacetsView(IReadOnlyList<FacetCountView> Franchises, IReadOnlyList<FacetCountView> Types);

public sealed record SearchResultView(IReadOnlyList<ProductListItemView> Items, int TotalCount, SearchFacetsView Facets);

public sealed record AuthView(
    Guid UserId,
    string Email,
    IReadOnlyList<string> Roles,
    string AccessToken,
    string RefreshToken);

public sealed record CartView(Guid Id, IReadOnlyList<CartItemView> Items, int TotalItems, decimal Subtotal);

public sealed record CartItemView(Guid Id, Guid ProductVariantId, int Quantity, decimal LineTotal);

public sealed record OrderView(
    Guid Id,
    string OrderNumber,
    string StatusName,
    decimal Total);

public sealed record OrderHistoryView(IReadOnlyList<OrderView> Items, int TotalCount);
