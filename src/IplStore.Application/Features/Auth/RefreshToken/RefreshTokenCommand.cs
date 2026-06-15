using FluentValidation;
using IplStore.Application.Common.Abstractions;
using IplStore.Shared;
using MediatR;

namespace IplStore.Application.Features.Auth.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthResponse>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly IIdentityService _identity;
    private readonly IJwtTokenService _jwt;

    public RefreshTokenCommandHandler(IIdentityService identity, IJwtTokenService jwt)
    {
        _identity = identity;
        _jwt = jwt;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var validate = await _identity.ValidateRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (validate.IsFailure) return validate.Error;

        var user = validate.Value;

        // Rotate: revoke the old refresh token, issue a fresh pair.
        await _identity.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
        var tokens = _jwt.GenerateTokens(user.Id, user.Email, user.Roles);
        await _identity.StoreRefreshTokenAsync(user.Id, tokens.RefreshToken,
            tokens.RefreshTokenExpiresAtUtc, cancellationToken);

        return new AuthResponse(user.Id, user.Email, user.FullName, user.Roles,
            tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAtUtc);
    }
}
