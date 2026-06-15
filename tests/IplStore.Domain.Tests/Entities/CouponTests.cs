using IplStore.Domain.Entities;
using IplStore.Domain.Enums;
using IplStore.Domain.ValueObjects;

namespace IplStore.Domain.Tests.Entities;

public class CouponTests
{
    private static Coupon PercentageCoupon(decimal percent = 10, decimal? minOrder = null, decimal? maxDiscount = null, int usageLimit = 100)
        => Coupon.Create("IPL10", CouponType.Percentage, percent,
            minOrder is null ? null : Money.From(minOrder.Value),
            maxDiscount is null ? null : Money.From(maxDiscount.Value),
            DateTime.UtcNow.AddDays(7), usageLimit).Value;

    [Fact]
    public void ComputeDiscount_Percentage_ReturnsCorrectAmount()
    {
        var coupon = PercentageCoupon(percent: 10);

        var result = coupon.ComputeDiscount(Money.From(1000m));

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(100m);
    }

    [Fact]
    public void ComputeDiscount_FixedAmount_ReturnsFlatValue()
    {
        var coupon = Coupon.Create("FLAT200", CouponType.FixedAmount, 200m, null, null,
            DateTime.UtcNow.AddDays(7), 100).Value;

        var result = coupon.ComputeDiscount(Money.From(1000m));

        result.Value.Amount.Should().Be(200m);
    }

    [Fact]
    public void ComputeDiscount_RespectsMaxDiscountCap()
    {
        var coupon = PercentageCoupon(percent: 50, maxDiscount: 300m);

        var result = coupon.ComputeDiscount(Money.From(1000m));

        result.Value.Amount.Should().Be(300m); // 50% would be 500, capped at 300
    }

    [Fact]
    public void ComputeDiscount_BelowMinOrderValue_ReturnsError()
    {
        var coupon = PercentageCoupon(minOrder: 2000m);

        var result = coupon.ComputeDiscount(Money.From(1000m));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("coupon.min_order_not_met");
    }

    [Fact]
    public void ComputeDiscount_NeverExceedsSubtotal()
    {
        var coupon = Coupon.Create("HUGE", CouponType.FixedAmount, 5000m, null, null,
            DateTime.UtcNow.AddDays(7), 100).Value;

        var result = coupon.ComputeDiscount(Money.From(1000m));

        result.Value.Amount.Should().Be(1000m);
    }

    [Fact]
    public void ComputeDiscount_WhenUsageExhausted_ReturnsError()
    {
        var coupon = PercentageCoupon(usageLimit: 1);
        coupon.IncrementUsage();

        var result = coupon.ComputeDiscount(Money.From(1000m));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("coupon.usage_exceeded");
    }

    [Fact]
    public void Create_WithPastExpiry_ReturnsError()
    {
        var result = Coupon.Create("OLD", CouponType.Percentage, 10m, null, null,
            DateTime.UtcNow.AddDays(-1), 100);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("coupon.already_expired");
    }

    [Fact]
    public void Create_PercentageOver100_ReturnsError()
    {
        var result = Coupon.Create("BAD", CouponType.Percentage, 150m, null, null,
            DateTime.UtcNow.AddDays(7), 100);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("coupon.percent_too_high");
    }
}
