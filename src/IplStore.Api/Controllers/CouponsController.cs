using Asp.Versioning;
using IplStore.Api.Common;
using IplStore.Application.Features.Coupons.CreateCoupon;
using IplStore.Application.Features.Coupons.ListCoupons;
using IplStore.Application.Features.Coupons.ValidateCoupon;
using IplStore.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/coupons")]
[ApiVersion("1.0")]
public sealed class CouponsController : ApiControllerBase
{
    private readonly ISender _sender;

    public CouponsController(ISender sender) => _sender = sender;

    /// <summary>Preview a coupon's discount against a given cart total. Public.</summary>
    [HttpPost("validate")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate([FromBody] ValidateCouponRequest body, CancellationToken ct)
        => HandleResult(await _sender.Send(new ValidateCouponQuery(body.Code, body.CartTotal), ct));

    /// <summary>List all coupons. Admin only.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => HandleResult(await _sender.Send(new ListCouponsQuery(), ct));

    /// <summary>Create a coupon. Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateCouponCommand command, CancellationToken ct)
        => HandleResult(await _sender.Send(command, ct));
}

public sealed record ValidateCouponRequest(string Code, decimal CartTotal);
