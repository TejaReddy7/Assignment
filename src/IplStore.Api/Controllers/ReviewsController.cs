using Asp.Versioning;
using IplStore.Api.Common;
using IplStore.Application.Features.Reviews.AddReview;
using IplStore.Application.Features.Reviews.DeleteReview;
using IplStore.Application.Features.Reviews.GetReviews;
using IplStore.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/products/{productId:guid}/reviews")]
[ApiVersion("1.0")]
public sealed class ReviewsController : ApiControllerBase
{
    private readonly ISender _sender;

    public ReviewsController(ISender sender) => _sender = sender;

    /// <summary>List reviews for a product (paginated), with the aggregate rating.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid productId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        => HandleResult(await _sender.Send(new GetReviewsQuery(productId, page, pageSize), ct));

    /// <summary>
    /// Submit a review. Requires authentication and verified purchase of the product.
    /// One review per customer per product.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Customer + "," + Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add(Guid productId, [FromBody] AddReviewRequest body, CancellationToken ct)
        => HandleResult(await _sender.Send(new AddReviewCommand(productId, body.Rating, body.Title, body.Body), ct));

    /// <summary>Delete a review (author or admin only).</summary>
    [HttpDelete("{reviewId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid productId, Guid reviewId, CancellationToken ct)
        => HandleResult(await _sender.Send(new DeleteReviewCommand(productId, reviewId), ct));
}

public sealed record AddReviewRequest(int Rating, string Title, string Body);
