using IplStore.Domain.Entities;
using IplStore.Domain.ValueObjects;

namespace IplStore.Application.Features.Catalog;

/// <summary>
/// Hand-rolled mapping extensions. No AutoMapper on purpose — mappings are explicit,
/// compile-checked, trivially unit-testable, and free of reflection surprises.
/// </summary>
public static class CatalogMappings
{
    public static MoneyDto ToDto(this Money money) => new(money.Amount, money.Currency);

    public static FranchiseDto ToDto(this Franchise f) =>
        new(f.Id, f.Name, f.ShortCode, f.City, f.PrimaryColor, f.FoundedYear, f.LogoUrl);

    public static ProductVariantDto ToDto(this ProductVariant v, Money basePrice) =>
        new(v.Id, v.Sku, v.Size, v.Color, v.StockQuantity, v.IsInStock, v.EffectivePrice(basePrice).ToDto());

    public static ProductListItemDto ToListItemDto(this Product p)
    {
        var variants = p.Variants
            .OrderBy(v => v.Size)
            .Select(v => v.ToDto(p.BasePrice))
            .ToList();

        var defaultVariantId = p.Variants.FirstOrDefault(v => v.IsInStock)?.Id;

        return new ProductListItemDto(
            p.Id,
            p.Name,
            p.Slug.Value,
            p.Type,
            p.Type.ToString(),
            p.BasePrice.ToDto(),
            p.ImageUrl,
            p.AverageRating,
            p.ReviewCount,
            p.Variants.Any(v => v.IsInStock),
            p.FranchiseId,
            p.Franchise.Name,
            p.Franchise.ShortCode,
            defaultVariantId,
            variants);
    }

    public static ProductDetailsDto ToDetailsDto(this Product p) =>
        new(
            p.Id,
            p.Name,
            p.Slug.Value,
            p.Description,
            p.Type,
            p.Type.ToString(),
            p.BasePrice.ToDto(),
            p.ImageUrl,
            p.AverageRating,
            p.ReviewCount,
            p.IsActive,
            p.Franchise.ToDto(),
            p.Variants.OrderBy(v => v.Size).Select(v => v.ToDto(p.BasePrice)).ToList());
}
