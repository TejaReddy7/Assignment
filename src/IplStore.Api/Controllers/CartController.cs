using Asp.Versioning;
using IplStore.Api.Common;
using IplStore.Application.Features.Cart.AddCartItem;
using IplStore.Application.Features.Cart.ClearCart;
using IplStore.Application.Features.Cart.GetCart;
using IplStore.Application.Features.Cart.MergeCart;
using IplStore.Application.Features.Cart.RemoveCartItem;
using IplStore.Application.Features.Cart.UpdateCartItem;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/cart")]
[ApiVersion("1.0")]
[Authorize]
public sealed class CartController : ApiControllerBase
{
    private readonly ISender _sender;

    public CartController(ISender sender) => _sender = sender;

    /// <summary>Get the current user's cart (created on first access).</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => HandleResult(await _sender.Send(new GetCartQuery(), ct));

    /// <summary>Add a product variant to the cart (merges quantity if already present).</summary>
    [HttpPost("items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddItem(AddCartItemCommand command, CancellationToken ct)
        => HandleResult(await _sender.Send(command, ct));

    /// <summary>
    /// Atomically merge a guest cart (from localStorage) into the user's server cart.
    /// Called once right after login so the browser avoids N concurrent add calls.
    /// </summary>
    [HttpPost("merge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Merge(MergeCartCommand command, CancellationToken ct)
        => HandleResult(await _sender.Send(command, ct));

    /// <summary>Update a line quantity. Quantity 0 removes the line.</summary>
    [HttpPatch("items/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateItem(Guid itemId, [FromBody] UpdateQuantityRequest body, CancellationToken ct)
        => HandleResult(await _sender.Send(new UpdateCartItemCommand(itemId, body.Quantity), ct));

    /// <summary>Remove a line from the cart.</summary>
    [HttpDelete("items/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(Guid itemId, CancellationToken ct)
        => HandleResult(await _sender.Send(new RemoveCartItemCommand(itemId), ct));

    /// <summary>Empty the cart.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Clear(CancellationToken ct)
        => HandleResult(await _sender.Send(new ClearCartCommand(), ct));
}

public sealed record UpdateQuantityRequest(int Quantity);
