using IplStore.Domain.Entities;
using IplStore.Domain.Enums;
using IplStore.Domain.Events;
using IplStore.Domain.ValueObjects;

namespace IplStore.Domain.Tests.Entities;

public class OrderTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static Address SampleAddress() =>
        Address.Create("12 Wankhede Rd", null, "Mumbai", "Maharashtra", "400020", "India").Value;

    private static IReadOnlyList<OrderItem> SampleItems() =>
        new[] { OrderItem.Create(Guid.NewGuid(), Guid.NewGuid(), "MI Home Jersey", "MI-JSY-M", Money.From(1999m), 2).Value };

    private static Order PlaceSampleOrder(Money? discount = null) =>
        Order.Place(
            CustomerId,
            SampleAddress(),
            PaymentMethod.Upi,
            SampleItems(),
            shippingFee: Money.From(99m),
            discountAmount: discount ?? Money.Zero,
            couponCode: discount is null ? null : "IPL10",
            idempotencyKey: Guid.NewGuid().ToString(),
            orderNumberFactory: () => "ORD-2026-000001").Value;

    [Fact]
    public void Place_ComputesTotalsCorrectly()
    {
        var order = PlaceSampleOrder(discount: Money.From(200m));

        order.Subtotal.Amount.Should().Be(3998m);      // 1999 * 2
        order.DiscountAmount.Amount.Should().Be(200m);
        order.ShippingFee.Amount.Should().Be(99m);
        order.Total.Amount.Should().Be(3897m);          // 3998 - 200 + 99
    }

    [Fact]
    public void Place_RaisesOrderPlacedEvent()
    {
        var order = PlaceSampleOrder();

        order.DomainEvents.Should().ContainSingle(e => e is OrderPlacedEvent);
    }

    [Fact]
    public void Place_WithEmptyItems_ReturnsError()
    {
        var result = Order.Place(CustomerId, SampleAddress(), PaymentMethod.Upi,
            Array.Empty<OrderItem>(), Money.From(99m), Money.Zero, null,
            Guid.NewGuid().ToString(), () => "ORD-X");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("cart.empty");
    }

    [Fact]
    public void StateMachine_HappyPath_PendingToDelivered()
    {
        var order = PlaceSampleOrder();

        order.Confirm("txn_123").IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Confirmed);

        order.Ship().IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Shipped);

        order.MarkDelivered().IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void Ship_BeforeConfirm_IsRejected()
    {
        var order = PlaceSampleOrder();

        var result = order.Ship();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("order.invalid_state");
    }

    [Fact]
    public void Cancel_PendingOrder_Succeeds_AndRaisesRestockEvent()
    {
        var order = PlaceSampleOrder();

        var result = order.Cancel("Changed my mind");

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCancelledEvent);
    }

    [Fact]
    public void Cancel_ShippedOrder_IsRejected()
    {
        var order = PlaceSampleOrder();
        order.Confirm("txn_123");
        order.Ship();

        var result = order.Cancel("Too late");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("order.cannot_cancel");
    }

    [Fact]
    public void Cancel_CapturedPayment_SetsRefunded()
    {
        var order = PlaceSampleOrder();
        order.Confirm("txn_123");

        order.Cancel("Defective");

        order.PaymentStatus.Should().Be(PaymentStatus.Refunded);
    }
}
