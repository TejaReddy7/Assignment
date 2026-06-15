using FluentValidation;
using IplStore.Application.Common.Abstractions;
using IplStore.Shared;
using MediatR;

namespace IplStore.Application.Features.Auth.Register;

public sealed record RegisterCommand(string Email, string FullName, string Password)
    : IRequest<Result<AuthResponse>>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a special character.");
    }
}

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly IIdentityService _identity;
    private readonly IJwtTokenService _jwt;

    public RegisterCommandHandler(IIdentityService identity, IJwtTokenService jwt)
    {
        _identity = identity;
        _jwt = jwt;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var register = await _identity.RegisterAsync(request.Email, request.FullName, request.Password, cancellationToken);
        if (register.IsFailure) return register.Error;

        var user = register.Value;
        var tokens = _jwt.GenerateTokens(user.Id, user.Email, user.Roles);
        await _identity.StoreRefreshTokenAsync(user.Id, tokens.RefreshToken,
            tokens.RefreshTokenExpiresAtUtc, cancellationToken);

        return new AuthResponse(user.Id, user.Email, user.FullName, user.Roles,
            tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAtUtc);
    }
}
