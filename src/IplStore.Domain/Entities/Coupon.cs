using IplStore.Domain.Enums;
using IplStore.Domain.Errors;
using IplStore.Domain.Primitives;
using IplStore.Domain.ValueObjects;
using IplStore.Shared;

namespace IplStore.Domain.Entities;

public sealed class Coupon : Entity<Guid>, IAggregateRoot, IAuditable
{
    private Coupon() { } // EF

    private Coupon(Guid id, string code, CouponType type, decimal value, Money? minOrderValue,
        Money? maxDiscount, DateTime expiresAtUtc, int usageLimit)
        : base(id)
    {
        Code = code;
        Type = type;
        Value = value;
        MinOrderValue = minOrderValue;
        MaxDiscount = maxDiscount;
        ExpiresAtUtc = expiresAtUtc;
        UsageLimit = usageLimit;
        UsedCount = 0;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public string Code { get; private set; } = default!;
    public CouponType Type { get; private set; }
    public decimal Value { get; private set; } // percentage (0-100) or fixed amount
    public Money? MinOrderValue { get; private set; }
    public Money? MaxDiscount { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public int UsageLimit { get; private set; }
    public int UsedCount { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public static Result<Coupon> Create(string code, CouponType type, decimal value,
        Money? minOrderValue, Money? maxDiscount, DateTime expiresAtUtc, int usageLimit)
    {
        if (string.IsNullOrWhiteSpace(code)) return Error.Validation("coupon.code_required", "Code is required.");
        if (value <= 0) return Error.Validation("coupon.value_invalid", "Discount value must be greater than zero.");
        if (type == CouponType.Percentage && value > 100)
            return Error.Validation("coupon.percent_too_high", "Percentage cannot exceed 100.");
        if (expiresAtUtc <= DateTime.UtcNow) return Error.Validation("coupon.already_expired", "Expiry must be in the future.");
        if (usageLimit <= 0) return Error.Validation("coupon.usage_limit_invalid", "Usage limit must be greater than zero.");

        return new Coupon(Guid.NewGuid(), code.Trim().ToUpperInvariant(), type, value, minOrderValue, maxDiscount, expiresAtUtc, usageLimit);
    }

    /// <summary>
    /// Returns the discount amount for the given subtotal, or an error if the coupon is invalid for it.
    /// </summary>
    public Result<Money> ComputeDiscount(Money subtotal)
    {
        if (!IsActive) return DomainErrors.Coupon.NotFound;
        if (DateTime.UtcNow >= ExpiresAtUtc) return DomainErrors.Coupon.Expired;
        if (UsedCount >= UsageLimit) return DomainErrors.Coupon.UsageExceeded;
        if (MinOrderValue is { } min && subtotal < min) return DomainErrors.Coupon.MinOrderValueNotMet;

        var discount = Type switch
        {
            CouponType.Percentage => subtotal * (Value / 100m),
            CouponType.FixedAmount => Money.From(Value, subtotal.Currency),
            _ => Money.Zero
        };

        if (MaxDiscount is { } cap && discount > cap) discount = cap;
        if (discount > subtotal) discount = subtotal;

        return discount;
    }

    public void IncrementUsage()
    {
        UsedCount++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
