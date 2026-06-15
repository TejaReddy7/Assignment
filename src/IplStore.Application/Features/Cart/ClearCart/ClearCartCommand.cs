using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;

namespace IplStore.Application.Features.Cart.ClearCart;

public sealed record ClearCartCommand : IRequest<Result>;

public sealed class ClearCartCommandHandler : IRequestHandler<ClearCartCommand, Result>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ClearCartCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return Result.Failure(DomainErrors.Auth.InvalidCredentials);

        var cart = await CartLoader.GetOrCreateAsync(_db, customerId, cancellationToken);
        cart.Clear();
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
