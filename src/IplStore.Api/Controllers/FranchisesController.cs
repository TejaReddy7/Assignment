using Asp.Versioning;
using IplStore.Api.Common;
using IplStore.Application.Features.Catalog.GetFranchises;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/franchises")]
[ApiVersion("1.0")]
public sealed class FranchisesController : ApiControllerBase
{
    private readonly ISender _sender;

    public FranchisesController(ISender sender) => _sender = sender;

    /// <summary>List all IPL franchises (used for search filters and navigation).</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
        => HandleResult(await _sender.Send(new GetFranchisesQuery(), ct));
}
