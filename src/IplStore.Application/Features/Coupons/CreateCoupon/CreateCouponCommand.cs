using FluentValidation;
using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Entities;
using IplStore.Domain.Enums;
using IplStore.Domain.ValueObjects;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Coupons.CreateCoupon;

public sealed record CreateCouponCommand(
    string Code,
    CouponType Type,
    decimal Value,
    decimal? MinOrderValue,
    decimal? MaxDiscount,
    DateTime ExpiresAtUtc,
    int UsageLimit) : IRequest<Result<CouponDto>>;

public sealed class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Value).LessThanOrEqualTo(100)
            .When(x => x.Type == CouponType.Percentage)
            .WithMessage("Percentage discount cannot exceed 100.");
        RuleFor(x => x.ExpiresAtUtc).GreaterThan(DateTime.UtcNow);
        RuleFor(x => x.UsageLimit).GreaterThan(0);
    }
}

public sealed class CreateCouponCommandHandler : IRequestHandler<CreateCouponCommand, Result<CouponDto>>
{
    private readonly IAppDbContext _db;

    public CreateCouponCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Result<CouponDto>> Handle(CreateCouponCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var exists = await _db.Coupons.AnyAsync(c => c.Code == code, cancellationToken);
        if (exists) return Error.Conflict("coupon.code_taken", $"Coupon code '{code}' already exists.");

        var minOrder = request.MinOrderValue.HasValue ? Money.From(request.MinOrderValue.Value) : (Money?)null;
        var maxDiscount = request.MaxDiscount.HasValue ? Money.From(request.MaxDiscount.Value) : (Money?)null;

        var couponResult = Coupon.Create(code, request.Type, request.Value, minOrder, maxDiscount,
            request.ExpiresAtUtc, request.UsageLimit);
        if (couponResult.IsFailure) return couponResult.Error;

        var coupon = couponResult.Value;
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync(cancellationToken);

        return coupon.ToDto();
    }
}
