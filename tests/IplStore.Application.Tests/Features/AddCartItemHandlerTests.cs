using IplStore.Application.Features.Cart.AddCartItem;
using IplStore.Application.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Tests.Features;

public class AddCartItemHandlerTests
{
    [Fact]
    public async Task Add_ValidVariant_AddsLineToCart()
    {
        await using var harness = new SqliteTestHarness();
        var (_, variant) = await TestData.SeedProductAsync(harness.Context, stock: 10);
        var handler = new AddCartItemCommandHandler(harness.Context, new FakeCurrentUser(TestData.CustomerId));

        var result = await handler.Handle(new AddCartItemCommand(variant.Id, 2), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalItems.Should().Be(2);
        result.Value.Subtotal.Should().Be(3998m);
    }

    [Fact]
    public async Task Add_BeyondStock_ReturnsInsufficientStock()
    {
        await using var harness = new SqliteTestHarness();
        var (_, variant) = await TestData.SeedProductAsync(harness.Context, stock: 1);
        var handler = new AddCartItemCommandHandler(harness.Context, new FakeCurrentUser(TestData.CustomerId));

        var result = await handler.Handle(new AddCartItemCommand(variant.Id, 5), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("variant.insufficient_stock");
    }

    [Fact]
    public async Task Add_UnknownVariant_ReturnsNotFound()
    {
        await using var harness = new SqliteTestHarness();
        await TestData.SeedProductAsync(harness.Context);
        var handler = new AddCartItemCommandHandler(harness.Context, new FakeCurrentUser(TestData.CustomerId));

        var result = await handler.Handle(new AddCartItemCommand(Guid.NewGuid(), 1), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("variant.not_found");
    }

    [Fact]
    public async Task Add_Twice_MergesQuantity()
    {
        await using var harness = new SqliteTestHarness();
        var (_, variant) = await TestData.SeedProductAsync(harness.Context, stock: 10);
        var handler = new AddCartItemCommandHandler(harness.Context, new FakeCurrentUser(TestData.CustomerId));

        await handler.Handle(new AddCartItemCommand(variant.Id, 2), default);
        var result = await handler.Handle(new AddCartItemCommand(variant.Id, 3), default);

        result.Value.Items.Should().HaveCount(1);
        result.Value.TotalItems.Should().Be(5);
    }

    [Fact]
    public async Task Add_NewLineToAlreadyPersistedCart_InsertsNotUpdates()
    {
        // Regression: a customer adds variant A (creates + persists the cart), then later — in a
        // fresh request/DbContext — adds variant B. Because our entities use client-generated GUID
        // keys, EF can't tell the new line is new and would emit UPDATE ... WHERE Id=@x (0 rows →
        // DbUpdateConcurrencyException). The handler now explicitly marks new lines as Added.
        await using var harness = new SqliteTestHarness();
        var (_, variantA, variantB) = await TestData.SeedProductWithTwoVariantsAsync(harness.Context, stock: 20);

        // Request 1: add variant A on its own context.
        await using (var ctx1 = harness.NewContext())
        {
            var add1 = await new AddCartItemCommandHandler(ctx1, new FakeCurrentUser(TestData.CustomerId))
                .Handle(new AddCartItemCommand(variantA.Id, 1), default);
            add1.IsSuccess.Should().BeTrue();
        }

        // Request 2: add variant B on a brand-new context (cart already exists in the DB).
        await using var ctx2 = harness.NewContext();
        var add2 = await new AddCartItemCommandHandler(ctx2, new FakeCurrentUser(TestData.CustomerId))
            .Handle(new AddCartItemCommand(variantB.Id, 2), default);

        add2.IsSuccess.Should().BeTrue();
        add2.Value.Items.Should().HaveCount(2);
        add2.Value.TotalItems.Should().Be(3);
    }

    [Fact]
    public async Task Add_AfterVariantRowVersionAdvanced_DoesNotThrowConcurrency()
    {
        // The variant is read-only reference data in the cart flow, so advancing its RowVersion
        // (e.g. via an order) must not cause a later add-to-cart to throw.
        await using var harness = new SqliteTestHarness();
        var (_, variant) = await TestData.SeedProductAsync(harness.Context, stock: 10);

        await using (var ctx = harness.NewContext())
        {
            var tracked = await ctx.ProductVariants.FirstAsync(v => v.Id == variant.Id);
            tracked.Decrement(1); // marks Modified → SaveChanges bumps RowVersion 0 → 1
            await ctx.SaveChangesAsync();
        }

        await using var addCtx = harness.NewContext();
        var handler = new AddCartItemCommandHandler(addCtx, new FakeCurrentUser(TestData.CustomerId));

        var result = await handler.Handle(new AddCartItemCommand(variant.Id, 2), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalItems.Should().Be(2);
    }
}
