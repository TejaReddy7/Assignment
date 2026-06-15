using Asp.Versioning;
using IplStore.Api.Common;
using IplStore.Application.Features.Orders.CancelOrder;
using IplStore.Application.Features.Orders.GetOrderByNumber;
using IplStore.Application.Features.Orders.GetOrders;
using IplStore.Application.Features.Orders.PlaceOrder;
using IplStore.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/orders")]
[ApiVersion("1.0")]
[Authorize]
public sealed class OrdersController : ApiControllerBase
{
    private readonly ISender _sender;

    public OrdersController(ISender sender) => _sender = sender;

    /// <summary>
    /// Place an order from the current cart. Requires an Idempotency-Key header so retries
    /// don't create duplicate orders. Applies an optional coupon and processes payment.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Place(
        [FromBody] PlaceOrderRequest body,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var key = string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey;
        var command = new PlaceOrderCommand(body.ShippingAddress, body.PaymentMethod, body.CouponCode, key);
        var result = await _sender.Send(command, ct);
        return result.IsSuccess
            ? CreatedAtRoute("GetOrderByNumber", new { orderNumber = result.Value.OrderNumber }, result.Value)
            : Problem(result.Error);
    }

    /// <summary>Order history for the current user. Supports status filter + pagination.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] OrderStatus? status = null,
        CancellationToken ct = default)
        => HandleResult(await _sender.Send(new GetOrdersQuery(page, pageSize, status), ct));

    /// <summary>Order details by order number. Customers see only their own; admins see all.</summary>
    [HttpGet("{orderNumber}", Name = "GetOrderByNumber")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByNumber(string orderNumber, CancellationToken ct)
        => HandleResult(await _sender.Send(new GetOrderByNumberQuery(orderNumber), ct));

    /// <summary>Cancel an order (only while Pending or Confirmed). Restores stock.</summary>
    [HttpPost("{orderNumber}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(string orderNumber, [FromBody] CancelOrderRequest? body, CancellationToken ct)
        => HandleResult(await _sender.Send(new CancelOrderCommand(orderNumber, body?.Reason), ct));
}

public sealed record PlaceOrderRequest(
    Application.Features.Orders.AddressDto ShippingAddress,
    PaymentMethod PaymentMethod,
    string? CouponCode);

public sealed record CancelOrderRequest(string? Reason);
