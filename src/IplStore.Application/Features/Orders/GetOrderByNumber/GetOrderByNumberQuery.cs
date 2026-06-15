using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Orders.GetOrderByNumber;

public sealed record GetOrderByNumberQuery(string OrderNumber) : IRequest<Result<OrderDetailsDto>>;

public sealed class GetOrderByNumberQueryHandler
    : IRequestHandler<GetOrderByNumberQuery, Result<OrderDetailsDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetOrderByNumberQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<OrderDetailsDto>> Handle(
        GetOrderByNumberQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return DomainErrors.Auth.InvalidCredentials;

        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == request.OrderNumber, cancellationToken);

        if (order is null) return DomainErrors.Order.NotFound;

        // Resource-based authorization: customers see only their own orders; admins see all.
        if (order.CustomerId != customerId && !_currentUser.IsAdmin)
            return DomainErrors.Order.NotFound; // 404, not 403 — don't leak existence

        return order.ToDetailsDto();
    }
}
