using IplStore.Domain.Entities;

namespace IplStore.Application.Features.Coupons;

public static class CouponMappings
{
    public static CouponDto ToDto(this Coupon c) =>
        new(
            c.Id,
            c.Code,
            c.Type,
            c.Type.ToString(),
            c.Value,
            c.MinOrderValue?.Amount,
            c.MaxDiscount?.Amount,
            c.ExpiresAtUtc,
            c.UsageLimit,
            c.UsedCount,
            c.IsActive);
}
