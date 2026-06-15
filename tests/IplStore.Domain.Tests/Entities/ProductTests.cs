using IplStore.Domain.Entities;
using IplStore.Domain.Enums;
using IplStore.Domain.ValueObjects;

namespace IplStore.Domain.Tests.Entities;

public class ProductTests
{
    private static Product CreateProduct() =>
        Product.Create("MI Home Jersey 2026", "Official Mumbai Indians home jersey.",
            ProductType.Jersey, Guid.NewGuid(), Money.From(1999m), "https://img/mi.png").Value;

    [Fact]
    public void Create_GeneratesSlugFromName()
    {
        var product = CreateProduct();

        product.Slug.Value.Should().Be("mi-home-jersey-2026");
        product.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithZeroPrice_ReturnsError()
    {
        var result = Product.Create("Test", "Desc", ProductType.Cap, Guid.NewGuid(), Money.Zero, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("product.price_invalid");
    }

    [Fact]
    public void AddVariant_WithDuplicateSku_ReturnsConflict()
    {
        var product = CreateProduct();
        product.AddVariant("MI-JSY-M", "M", "Blue", 50);

        var result = product.AddVariant("mi-jsy-m", "L", "Blue", 30);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("variant.sku_taken");
    }

    [Fact]
    public void Variant_Decrement_BelowStock_Succeeds()
    {
        var product = CreateProduct();
        var variant = product.AddVariant("MI-JSY-M", "M", "Blue", 50).Value;

        var result = variant.Decrement(10);

        result.IsSuccess.Should().BeTrue();
        variant.StockQuantity.Should().Be(40);
    }

    [Fact]
    public void Variant_Decrement_ExceedingStock_ReturnsInsufficientStock()
    {
        var product = CreateProduct();
        var variant = product.AddVariant("MI-JSY-M", "M", "Blue", 5).Value;

        var result = variant.Decrement(10);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("variant.insufficient_stock");
        variant.StockQuantity.Should().Be(5);
    }

    [Fact]
    public void SoftDelete_DeactivatesAndFlags()
    {
        var product = CreateProduct();

        product.SoftDelete();

        product.IsDeleted.Should().BeTrue();
        product.IsActive.Should().BeFalse();
        product.DeletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void RecomputeRating_UpdatesDenormalizedFields_AndRaisesEvent()
    {
        var product = CreateProduct();

        product.RecomputeRating(4.333m, 3);

        product.AverageRating.Should().Be(4.33m);
        product.ReviewCount.Should().Be(3);
        product.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "ProductRatingUpdatedEvent");
    }
}
