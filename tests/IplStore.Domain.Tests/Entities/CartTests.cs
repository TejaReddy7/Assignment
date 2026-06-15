using IplStore.Domain.Entities;
using IplStore.Domain.ValueObjects;

namespace IplStore.Domain.Tests.Entities;

public class CartTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid VariantId = Guid.NewGuid();

    [Fact]
    public void AddOrMerge_NewItem_AddsLine()
    {
        var cart = Cart.CreateFor(CustomerId);

        var result = cart.AddOrMerge(ProductId, VariantId, "MI Home Jersey", null, Money.From(1999m), 2, availableStock: 10);

        result.IsSuccess.Should().BeTrue();
        cart.Items.Should().HaveCount(1);
        cart.TotalItems.Should().Be(2);
        cart.Subtotal.Amount.Should().Be(3998m);
    }

    [Fact]
    public void AddOrMerge_ExistingVariant_MergesQuantity()
    {
        var cart = Cart.CreateFor(CustomerId);
        cart.AddOrMerge(ProductId, VariantId, "MI Home Jersey", null, Money.From(1999m), 2, 10);

        cart.AddOrMerge(ProductId, VariantId, "MI Home Jersey", null, Money.From(1999m), 3, 10);

        cart.Items.Should().HaveCount(1);
        cart.TotalItems.Should().Be(5);
    }

    [Fact]
    public void AddOrMerge_ExceedingStock_ReturnsInsufficientStock()
    {
        var cart = Cart.CreateFor(CustomerId);

        var result = cart.AddOrMerge(ProductId, VariantId, "MI Home Jersey", null, Money.From(1999m), 5, availableStock: 3);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("variant.insufficient_stock");
    }

    [Fact]
    public void AddOrMerge_ExceedingPerLineLimit_ReturnsLimitError()
    {
        var cart = Cart.CreateFor(CustomerId);

        var result = cart.AddOrMerge(ProductId, VariantId, "MI Home Jersey", null, Money.From(1999m), 11, availableStock: 100);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("cart.qty_exceeds_limit");
    }

    [Fact]
    public void UpdateQuantity_ToZero_RemovesItem()
    {
        var cart = Cart.CreateFor(CustomerId);
        var add = cart.AddOrMerge(ProductId, VariantId, "MI Home Jersey", null, Money.From(1999m), 2, 10);

        var result = cart.UpdateQuantity(add.Value.Id, 0, availableStock: 10);

        result.IsSuccess.Should().BeTrue();
        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public void Remove_NonExistentItem_ReturnsItemNotFound()
    {
        var cart = Cart.CreateFor(CustomerId);

        var result = cart.Remove(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("cart.item_not_found");
    }

    [Fact]
    public void Clear_EmptiesCart()
    {
        var cart = Cart.CreateFor(CustomerId);
        cart.AddOrMerge(ProductId, VariantId, "MI Home Jersey", null, Money.From(1999m), 2, 10);

        cart.Clear();

        cart.Items.Should().BeEmpty();
        cart.Subtotal.Should().Be(Money.Zero);
    }
}
