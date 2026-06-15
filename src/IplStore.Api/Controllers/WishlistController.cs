using Asp.Versioning;
using IplStore.Api.Common;
using IplStore.Application.Features.Wishlist.AddToWishlist;
using IplStore.Application.Features.Wishlist.GetWishlist;
using IplStore.Application.Features.Wishlist.RemoveFromWishlist;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/wishlist")]
[ApiVersion("1.0")]
[Authorize]
public sealed class WishlistController : ApiControllerBase
{
    private readonly ISender _sender;

    public WishlistController(ISender sender) => _sender = sender;

    /// <summary>Get the current user's wishlist.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => HandleResult(await _sender.Send(new GetWishlistQuery(), ct));

    /// <summary>Add a product to the wishlist (idempotent).</summary>
    [HttpPost("{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(Guid productId, CancellationToken ct)
        => HandleResult(await _sender.Send(new AddToWishlistCommand(productId), ct));

    /// <summary>Remove a product from the wishlist (idempotent).</summary>
    [HttpDelete("{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid productId, CancellationToken ct)
        => HandleResult(await _sender.Send(new RemoveFromWishlistCommand(productId), ct));
}
