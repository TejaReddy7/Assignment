using IplStore.Application.Features.Cart.AddCartItem;
using IplStore.Application.Tests.Common;

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
}
