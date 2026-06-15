using IplStore.Domain.Enums;

namespace IplStore.Application.Features.Coupons;

public sealed record CouponDto(
    Guid Id,
    string Code,
    CouponType Type,
    string TypeName,
    decimal Value,
    decimal? MinOrderValue,
    decimal? MaxDiscount,
    DateTime ExpiresAtUtc,
    int UsageLimit,
    int UsedCount,
    bool IsActive);

public sealed record CouponValidationResult(
    bool IsValid,
    string Code,
    decimal Discount,
    decimal NewTotal,
    string? Reason);
