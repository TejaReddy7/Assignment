namespace IplStore.Application.Features.Catalog.Featured;

public sealed record FeaturedProductDto(
    string Badge,
    string Reason,
    double DemandScore,
    ProductListItemDto Product);
