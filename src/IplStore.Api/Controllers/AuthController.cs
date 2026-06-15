using IplStore.Api.Common;
using IplStore.Application.Features.Auth.GetProfile;
using IplStore.Application.Features.Auth.Login;
using IplStore.Application.Features.Auth.Logout;
using IplStore.Application.Features.Auth.Register;
using IplStore.Application.Features.Auth.RefreshToken;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IplStore.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[ApiVersion("1.0")]
public sealed class AuthController : ApiControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender) => _sender = sender;

    /// <summary>Register a new customer account and receive auth tokens.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterCommand command, CancellationToken ct)
        => HandleResult(await _sender.Send(command, ct));

    /// <summary>Authenticate with email + password.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken ct)
        => HandleResult(await _sender.Send(command, ct));

    /// <summary>Exchange a valid refresh token for a new token pair (rotation).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command, CancellationToken ct)
        => HandleResult(await _sender.Send(command, ct));

    /// <summary>Revoke the current refresh token.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken ct)
        => HandleResult(await _sender.Send(command, ct));

    /// <summary>Get the authenticated user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
        => HandleResult(await _sender.Send(new GetProfileQuery(), ct));
}
