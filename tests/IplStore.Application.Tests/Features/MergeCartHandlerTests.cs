using IplStore.Application.Features.Cart.AddCartItem;
using IplStore.Application.Features.Cart.MergeCart;
using IplStore.Application.Tests.Common;

namespace IplStore.Application.Tests.Features;

public class MergeCartHandlerTests
{
    [Fact]
    public async Task Merge_IntoEmptyCart_AddsAllLines()
    {
        await using var harness = new SqliteTestHarness();
        var (_, variant) = await TestData.SeedProductAsync(harness.Context, stock: 10, price: 500m);
        var handler = new MergeCartCommandHandler(harness.Context, new FakeCurrentUser(TestData.CustomerId));

        var result = await handler.Handle(
            new MergeCartCommand(new[] { new MergeCartLine(variant.Id, 3) }), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalItems.Should().Be(3);
        result.Value.Subtotal.Should().Be(1500m);
    }

    [Fact]
    public async Task Merge_IntoExistingCart_CombinesQuantities()
    {
        await using var harness = new SqliteTestHarness();
        var (_, variant) = await TestData.SeedProductAsync(harness.Context, stock: 10, price: 500m);
        var currentUser = new FakeCurrentUser(TestData.CustomerId);

        // Existing server cart already has 2 of this variant.
        await new AddCartItemCommandHandler(harness.Context, currentUser)
            .Handle(new AddCartItemCommand(variant.Id, 2), default);

        // Guest cart (being merged) adds 3 more of the same variant.
        var result = await new MergeCartCommandHandler(harness.Context, currentUser)
            .Handle(new MergeCartCommand(new[] { new MergeCartLine(variant.Id, 3) }), default);

        result.Value.Items.Should().HaveCount(1);
        result.Value.TotalItems.Should().Be(5);
    }

    [Fact]
    public async Task Merge_EmptyList_ReturnsCartUnchanged()
    {
        await using var harness = new SqliteTestHarness();
        await TestData.SeedProductAsync(harness.Context);
        var handler = new MergeCartCommandHandler(harness.Context, new FakeCurrentUser(TestData.CustomerId));

        var result = await handler.Handle(new MergeCartCommand(Array.Empty<MergeCartLine>()), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task Merge_BeyondStock_SkipsThatLine_DoesNotThrow()
    {
        await using var harness = new SqliteTestHarness();
        var (_, variant) = await TestData.SeedProductAsync(harness.Context, stock: 2);
        var handler = new MergeCartCommandHandler(harness.Context, new FakeCurrentUser(TestData.CustomerId));

        // Requesting 5 but only 2 in stock — line is skipped (best-effort merge), no exception.
        var result = await handler.Handle(
            new MergeCartCommand(new[] { new MergeCartLine(variant.Id, 5) }), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalItems.Should().Be(0);
    }
}
