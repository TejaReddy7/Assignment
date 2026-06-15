using IplStore.Domain.Entities;
using IplStore.Domain.ValueObjects;

namespace IplStore.Application.Features.Orders;

public static class OrderMappings
{
    public static AddressDto ToDto(this Address a) =>
        new(a.Line1, a.Line2, a.City, a.State, a.PostalCode, a.Country);

    public static OrderItemDto ToDto(this OrderItem i) =>
        new(i.ProductId, i.ProductVariantId, i.ProductSnapshot, i.SkuSnapshot,
            i.UnitPrice.Amount, i.Quantity, i.LineTotal.Amount);

    public static OrderSummaryDto ToSummaryDto(this Order o) =>
        new(
            o.Id,
            o.OrderNumber,
            o.Status,
            o.Status.ToString(),
            o.PaymentStatus,
            o.Total.Amount,
            o.Total.Currency,
            o.Items.Sum(i => i.Quantity),
            o.PlacedAtUtc);

    public static OrderDetailsDto ToDetailsDto(this Order o) =>
        new(
            o.Id,
            o.OrderNumber,
            o.Status,
            o.Status.ToString(),
            o.PaymentStatus,
            o.PaymentMethod,
            o.PaymentTransactionId,
            o.ShippingAddress.ToDto(),
            o.Items.Select(i => i.ToDto()).ToList(),
            o.Subtotal.Amount,
            o.DiscountAmount.Amount,
            o.ShippingFee.Amount,
            o.Total.Amount,
            o.Total.Currency,
            o.CouponCode,
            o.PlacedAtUtc,
            o.ConfirmedAtUtc,
            o.ShippedAtUtc,
            o.DeliveredAtUtc,
            o.CancelledAtUtc,
            o.CancellationReason);
}
