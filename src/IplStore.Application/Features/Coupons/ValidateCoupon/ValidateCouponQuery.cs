using IplStore.Application.Common.Abstractions;
using IplStore.Domain.ValueObjects;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Coupons.ValidateCoupon;

public sealed record ValidateCouponQuery(string Code, decimal CartTotal) : IRequest<Result<CouponValidationResult>>;

public sealed class ValidateCouponQueryHandler : IRequestHandler<ValidateCouponQuery, Result<CouponValidationResult>>
{
    private readonly IAppDbContext _db;

    public ValidateCouponQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<CouponValidationResult>> Handle(ValidateCouponQuery request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);

        if (coupon is null)
            return new CouponValidationResult(false, code, 0, request.CartTotal, "Coupon code is invalid.");

        var subtotal = Money.From(request.CartTotal);
        var discountResult = coupon.ComputeDiscount(subtotal);

        if (discountResult.IsFailure)
            return new CouponValidationResult(false, code, 0, request.CartTotal, discountResult.Error.Description);

        var discount = discountResult.Value;
        return new CouponValidationResult(true, code, discount.Amount, (subtotal - discount).Amount, null);
    }
}
