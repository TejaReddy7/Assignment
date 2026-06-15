using IplStore.Application.Common.Abstractions;
using IplStore.Shared;
using MediatR;

namespace IplStore.Application.Features.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IIdentityService _identity;

    public LogoutCommandHandler(IIdentityService identity) => _identity = identity;

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            await _identity.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);

        return Result.Success();
    }
}
