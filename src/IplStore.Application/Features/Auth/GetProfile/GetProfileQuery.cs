using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;

namespace IplStore.Application.Features.Auth.GetProfile;

public sealed record GetProfileQuery : IRequest<Result<UserProfileResponse>>;

public sealed class GetProfileQueryHandler : IRequestHandler<GetProfileQuery, Result<UserProfileResponse>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityService _identity;

    public GetProfileQueryHandler(ICurrentUser currentUser, IIdentityService identity)
    {
        _currentUser = currentUser;
        _identity = identity;
    }

    public async Task<Result<UserProfileResponse>> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
            return DomainErrors.Auth.InvalidCredentials;

        var find = await _identity.FindByIdAsync(userId, cancellationToken);
        if (find.IsFailure) return find.Error;

        var user = find.Value;
        return new UserProfileResponse(user.Id, user.Email, user.FullName, user.Roles);
    }
}
