using IplStore.Domain.Enums;

namespace IplStore.Application.Features.Catalog;

public sealed record MoneyDto(decimal Amount, string Currency);

public sealed record FranchiseDto(
    Guid Id,
    string Name,
    string ShortCode,
    string City,
    string PrimaryColor,
    int FoundedYear,
    string? LogoUrl);

public sealed record ProductVariantDto(
    Guid Id,
    string Sku,
    string? Size,
    string? Color,
    int StockQuantity,
    bool InStock,
    MoneyDto Price);

public sealed record ProductListItemDto(
    Guid Id,
    string Name,
    string Slug,
    ProductType Type,
    string TypeName,
    MoneyDto BasePrice,
    string? ImageUrl,
    decimal AverageRating,
    int ReviewCount,
    bool InStock,
    Guid FranchiseId,
    string FranchiseName,
    string FranchiseShortCode);

public sealed record ProductDetailsDto(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    ProductType Type,
    string TypeName,
    MoneyDto BasePrice,
    string? ImageUrl,
    decimal AverageRating,
    int ReviewCount,
    bool IsActive,
    FranchiseDto Franchise,
    IReadOnlyList<ProductVariantDto> Variants);
