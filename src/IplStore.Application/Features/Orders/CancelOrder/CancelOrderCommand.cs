using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Orders.CancelOrder;

public sealed record CancelOrderCommand(string OrderNumber, string? Reason) : IRequest<Result<OrderDetailsDto>>;

public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, Result<OrderDetailsDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ICacheService _cache;

    public CancelOrderCommandHandler(IAppDbContext db, ICurrentUser currentUser, ICacheService cache)
    {
        _db = db;
        _currentUser = currentUser;
        _cache = cache;
    }

    public async Task<Result<OrderDetailsDto>> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return DomainErrors.Auth.InvalidCredentials;

        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == request.OrderNumber, cancellationToken);

        if (order is null) return DomainErrors.Order.NotFound;
        if (order.CustomerId != customerId && !_currentUser.IsAdmin)
            return DomainErrors.Order.NotFound;

        // Cancel raises OrderCancelledEvent → restock handler returns inventory atomically on save.
        var cancel = order.Cancel(request.Reason ?? "Cancelled by customer.");
        if (cancel.IsFailure) return cancel.Error;

        await _db.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByPrefixAsync(Common.CacheKeys.ProductPrefix, cancellationToken);
        return order.ToDetailsDto();
    }
}
