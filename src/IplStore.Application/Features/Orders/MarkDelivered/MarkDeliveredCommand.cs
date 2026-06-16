using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Orders.MarkDelivered;

public sealed record MarkDeliveredCommand(string OrderNumber) : IRequest<Result<OrderDetailsDto>>;

public sealed class MarkDeliveredCommandHandler : IRequestHandler<MarkDeliveredCommand, Result<OrderDetailsDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public MarkDeliveredCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<OrderDetailsDto>> Handle(MarkDeliveredCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin) return DomainErrors.Auth.Forbidden;

        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == request.OrderNumber, cancellationToken);
        if (order is null) return DomainErrors.Order.NotFound;

        var deliver = order.MarkDelivered();
        if (deliver.IsFailure) return deliver.Error;

        await _db.SaveChangesAsync(cancellationToken);
        return order.ToDetailsDto();
    }
}
