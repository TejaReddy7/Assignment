using IplStore.Domain.Enums;

namespace IplStore.Application.Features.Orders;

public sealed record AddressDto(
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string Country);

public sealed record OrderItemDto(
    Guid ProductId,
    Guid ProductVariantId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public sealed record OrderSummaryDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    string StatusName,
    PaymentStatus PaymentStatus,
    decimal Total,
    string Currency,
    int ItemCount,
    DateTime PlacedAtUtc,
    string? CustomerEmail = null,
    string? CustomerName = null);

public sealed record OrderDetailsDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    string StatusName,
    PaymentStatus PaymentStatus,
    PaymentMethod PaymentMethod,
    string? PaymentTransactionId,
    AddressDto ShippingAddress,
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal ShippingFee,
    decimal Total,
    string Currency,
    string? CouponCode,
    DateTime PlacedAtUtc,
    DateTime? ConfirmedAtUtc,
    DateTime? ShippedAtUtc,
    DateTime? DeliveredAtUtc,
    DateTime? CancelledAtUtc,
    string? CancellationReason);
