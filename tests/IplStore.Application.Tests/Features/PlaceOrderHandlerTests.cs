using IplStore.Application.Features.Cart.AddCartItem;
using IplStore.Application.Features.Orders;
using IplStore.Application.Features.Orders.PlaceOrder;
using IplStore.Application.Tests.Common;
using IplStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IplStore.Application.Tests.Features;

public class PlaceOrderHandlerTests
{
    private static readonly AddressDto Address =
        new("12 Wankhede Rd", null, "Mumbai", "Maharashtra", "400020", "India");

    private static async Task AddToCartAsync(Common.SqliteTestHarness harness, Guid variantId, int qty)
    {
        var add = new AddCartItemCommandHandler(harness.Context, new FakeCurrentUser(TestData.CustomerId));
        (await add.Handle(new AddCartItemCommand(variantId, qty), default)).IsSuccess.Should().BeTrue();
    }

    private static PlaceOrderCommandHandler CreateHandler(
        Common.SqliteTestHarness harness, bool paymentSucceeds = true) =>
        new(
            harness.Context,
            new FakeCurrentUser(TestData.CustomerId),
            new FakePaymentGateway(paymentSucceeds),
            new FakeOrderNumberGenerator(),
            new NoOpCache(),
            NullLogger<PlaceOrderCommandHandler>.Instance);

    [Fact]
    public async Task Place_HappyPath_CreatesConfirmedOrder_AndDecrementsStock()
    {
        await using var harness = new SqliteTestHarness();
        var (_, variant) = await TestData.SeedProductAsync(harness.Context, stock: 10, price: 1000m);
        await AddToCartAsync(harness, variant.Id, 2);

        var result = await CreateHandler(harness).Handle(
            new PlaceOrderCommand(Address, PaymentMethod.Upi, null, Guid.NewGuid().ToString()), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(OrderStatus.Confirmed);
        result.Value.Subtotal.Should().Be(2000m);
        result.Value.Total.Should().Be(2099m); // 2000 + 99 shipping

        var stock = await harness.NewContext().ProductVariants
            .Where(v => v.Id == variant.Id).Select(v => v.StockQuantity).FirstAsync();
        stock.Should().Be(8);
    }

    [Fact]
    public async Task Place_WithCoupon_AppliesDiscount()
    {
        await using var harness = new SqliteTestHarness();
        var (_, variant) = await TestData.SeedProductAsync(harness.Context, stock: 10, price: 1000m);
        await TestData.SeedCouponAsync(harness.Context, "SAVE10", percent: 10);
        await AddToCartAsync(harness, variant.Id, 2);

        var result = await CreateHandler(harness).Handle(
            new PlaceOrderCommand(Address, PaymentMethod.Upi, "SAVE10", Guid.NewGuid().ToString()), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.DiscountAmount.Should().Be(200m); // 10% of 2000
        result.Value.Total.Should().Be(1899m);          // 2000 - 200 + 99
    }

    [Fact]
    public async Task Place_FreeShippingOverThreshold()
    {
        await using var harness = new SqliteTestHarness();
        var (_, variant) = await TestData.SeedProductAsync(harness.Context, stock: 10, price: 3000m);
        await AddToCartAsync(harness, variant.Id, 2); // 6000 > 4999 threshold

        var result = await CreateHandler(harness).Handle(
            new PlaceOrderCommand(Address, PaymentMethod.Upi, null, Guid.NewGuid().ToString()), default);

        result.Value.ShippingFee.Should().Be(0m);
        result.Value.Total.Should().Be(6000m);
    }

    [Fact]
    public async Task Place_EmptyCart_ReturnsError()
    {
        await using var harness = new SqliteTestHarness();
        await TestData.SeedProductAsync(harness.Context);

        var result = await CreateHandler(harness).Handle(
            new PlaceOrderCommand(Address, PaymentMethod.Upi, null, Guid.NewGuid().ToString()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("cart.empty");
    }

    [Fact]
    public async Task Place_PaymentDeclined_ReturnsError_AndDoesNotPersistOrder()
    {
        await using var harness = new SqliteTestHarness();
        var (_, variant) = await TestData.SeedProductAsync(harness.Context, stock: 10);
        await AddToCartAsync(harness, variant.Id, 1);

        var result = await CreateHandler(harness, paymentSucceeds: false).Handle(
            new PlaceOrderCommand(Address, PaymentMethod.Upi, null, Guid.NewGuid().ToString()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("order.payment_failed");

        var orderCount = await harness.NewContext().Orders.CountAsync();
        orderCount.Should().Be(0, "the transaction must roll back on payment failure");

        var stock = await harness.NewContext().ProductVariants
            .Where(v => v.Id == variant.Id).Select(v => v.StockQuantity).FirstAsync();
        stock.Should().Be(10, "stock must not be decremented when payment fails");
    }

    [Fact]
    public async Task Place_SameIdempotencyKey_ReturnsSameOrder_WithoutDoubleCharge()
    {
        await using var harness = new SqliteTestHarness();
        var (_, variant) = await TestData.SeedProductAsync(harness.Context, stock: 10);
        await AddToCartAsync(harness, variant.Id, 2);
        var key = Guid.NewGuid().ToString();

        var first = await CreateHandler(harness).Handle(
            new PlaceOrderCommand(Address, PaymentMethod.Upi, null, key), default);
        var replay = await CreateHandler(harness).Handle(
            new PlaceOrderCommand(Address, PaymentMethod.Upi, null, key), default);

        first.IsSuccess.Should().BeTrue();
        replay.IsSuccess.Should().BeTrue();
        replay.Value.OrderNumber.Should().Be(first.Value.OrderNumber);

        var orderCount = await harness.NewContext().Orders.CountAsync();
        orderCount.Should().Be(1, "idempotency must prevent a duplicate order");

        var stock = await harness.NewContext().ProductVariants
            .Where(v => v.Id == variant.Id).Select(v => v.StockQuantity).FirstAsync();
        stock.Should().Be(8, "stock should be decremented exactly once");
    }
}
