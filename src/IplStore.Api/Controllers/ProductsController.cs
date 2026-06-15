using Asp.Versioning;
using IplStore.Api.Common;
using IplStore.Application.Features.Catalog.CreateProduct;
using IplStore.Application.Features.Catalog.DeleteProduct;
using IplStore.Application.Features.Catalog.GetProductBySlug;
using IplStore.Application.Features.Catalog.GetProducts;
using IplStore.Application.Features.Catalog.Search;
using IplStore.Application.Features.Catalog.UpdateProduct;
using IplStore.Domain.Enums;
using IplStore.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/products")]
[ApiVersion("1.0")]
public sealed class ProductsController : ApiControllerBase
{
    private readonly ISender _sender;

    public ProductsController(ISender sender) => _sender = sender;

    /// <summary>Paginated product list. Supports sortBy=name|price|rating|newest and sortDir=asc|desc.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = "name",
        [FromQuery] string? sortDir = "asc",
        CancellationToken ct = default)
        => HandleResult(await _sender.Send(new GetProductsQuery(page, pageSize, sortBy, sortDir), ct));

    /// <summary>
    /// Faceted search by name/description/franchise text, with filters for franchise (short code),
    /// type, price range, and stock. Returns results plus facet counts for filter sidebars.
    /// </summary>
    [HttpGet("search")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? q = null,
        [FromQuery] string? franchise = null,
        [FromQuery] ProductType? type = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] bool inStockOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = "relevance",
        [FromQuery] string? sortDir = "desc",
        CancellationToken ct = default)
        => HandleResult(await _sender.Send(
            new SearchProductsQuery(q, franchise, type, minPrice, maxPrice, inStockOnly, page, pageSize, sortBy, sortDir), ct));

    /// <summary>Product details by slug (e.g. mumbai-indians-home-jersey-2026).</summary>
    [HttpGet("{slug}", Name = "GetProductBySlug")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
        => HandleResult(await _sender.Send(new GetProductBySlugQuery(slug), ct));

    /// <summary>Create a product with variants. Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(CreateProductCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return result.IsSuccess
            ? CreatedAtRoute("GetProductBySlug", new { slug = result.Value.Slug }, result.Value)
            : Problem(result.Error);
    }

    /// <summary>Update a product's core fields. Admin only.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdateProductCommand command, CancellationToken ct)
    {
        if (id != command.Id) return BadRequest("Route id and body id must match.");
        return HandleResult(await _sender.Send(command, ct));
    }

    /// <summary>Soft-delete a product. Admin only.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleResult(await _sender.Send(new DeleteProductCommand(id), ct));
}
