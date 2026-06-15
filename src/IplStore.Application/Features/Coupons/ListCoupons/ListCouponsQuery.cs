using IplStore.Application.Common.Abstractions;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Coupons.ListCoupons;

public sealed record ListCouponsQuery : IRequest<Result<IReadOnlyList<CouponDto>>>;

public sealed class ListCouponsQueryHandler : IRequestHandler<ListCouponsQuery, Result<IReadOnlyList<CouponDto>>>
{
    private readonly IAppDbContext _db;

    public ListCouponsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<CouponDto>>> Handle(ListCouponsQuery request, CancellationToken cancellationToken)
    {
        var coupons = await _db.Coupons
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        IReadOnlyList<CouponDto> dtos = coupons.Select(c => c.ToDto()).ToList();
        return Result.Success(dtos);
    }
}
