using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;

namespace IplStore.Application.Features.Cart.GetCart;

public sealed record GetCartQuery : IRequest<Result<CartDto>>;

public sealed class GetCartQueryHandler : IRequestHandler<GetCartQuery, Result<CartDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetCartQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<CartDto>> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return DomainErrors.Auth.InvalidCredentials;

        var cart = await CartLoader.GetOrCreateAsync(_db, customerId, cancellationToken);
        return cart.ToDto();
    }
}
