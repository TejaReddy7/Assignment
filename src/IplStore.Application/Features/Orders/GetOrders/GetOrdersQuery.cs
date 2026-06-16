using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Enums;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Orders.GetOrders;

public sealed record GetOrdersQuery(int Page = 1, int PageSize = 10, OrderStatus? Status = null)
    : IRequest<Result<PagedResult<OrderSummaryDto>>>;

public sealed class GetOrdersQueryHandler
    : IRequestHandler<GetOrdersQuery, Result<PagedResult<OrderSummaryDto>>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IIdentityService _identity;

    public GetOrdersQueryHandler(IAppDbContext db, ICurrentUser currentUser, IIdentityService identity)
    {
        _db = db;
        _currentUser = currentUser;
        _identity = identity;
    }

    public async Task<Result<PagedResult<OrderSummaryDto>>> Handle(
        GetOrdersQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return DomainErrors.Auth.InvalidCredentials;

        var pagination = new PaginationParams { Page = request.Page, PageSize = request.PageSize };

        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .AsQueryable();

        // Admins see all orders; customers see only their own.
        if (!_currentUser.IsAdmin)
            query = query.Where(o => o.CustomerId == customerId);

        if (request.Status is { } status)
            query = query.Where(o => o.Status == status);

        query = query.OrderByDescending(o => o.PlacedAtUtc);

        var total = await query.CountAsync(cancellationToken);
        var orders = await query
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        IReadOnlyDictionary<Guid, UserDescriptor> usersById = new Dictionary<Guid, UserDescriptor>();
        if (_currentUser.IsAdmin && orders.Count > 0)
            usersById = await _identity.GetUsersByIdsAsync(
                orders.Select(o => o.CustomerId), cancellationToken);

        var items = orders.Select(o =>
        {
            var dto = o.ToSummaryDto();
            if (_currentUser.IsAdmin && usersById.TryGetValue(o.CustomerId, out var user))
                dto = dto with { CustomerEmail = user.Email, CustomerName = user.FullName };
            return dto;
        }).ToList();

        return new PagedResult<OrderSummaryDto>(items, pagination.Page, pagination.PageSize, total);
    }
}

