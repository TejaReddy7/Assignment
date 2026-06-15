using FluentValidation;
using IplStore.Application.Common.Abstractions;
using IplStore.Shared;
using MediatR;

namespace IplStore.Application.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly IIdentityService _identity;
    private readonly IJwtTokenService _jwt;

    public LoginCommandHandler(IIdentityService identity, IJwtTokenService jwt)
    {
        _identity = identity;
        _jwt = jwt;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validate = await _identity.ValidateCredentialsAsync(request.Email, request.Password, cancellationToken);
        if (validate.IsFailure) return validate.Error;

        var user = validate.Value;
        var tokens = _jwt.GenerateTokens(user.Id, user.Email, user.Roles);
        await _identity.StoreRefreshTokenAsync(user.Id, tokens.RefreshToken,
            tokens.RefreshTokenExpiresAtUtc, cancellationToken);

        return new AuthResponse(user.Id, user.Email, user.FullName, user.Roles,
            tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAtUtc);
    }
}
