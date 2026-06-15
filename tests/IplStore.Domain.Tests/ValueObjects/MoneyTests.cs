using IplStore.Domain.ValueObjects;

namespace IplStore.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Create_WithNegativeAmount_ReturnsValidationError()
    {
        var result = Money.Create(-1m);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("money.negative");
    }

    [Theory]
    [InlineData("US")]
    [InlineData("RUPEE")]
    [InlineData("")]
    public void Create_WithInvalidCurrency_ReturnsValidationError(string currency)
    {
        var result = Money.Create(10m, currency);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("money.currency_invalid");
    }

    [Fact]
    public void Create_RoundsToTwoDecimals_AwayFromZero()
    {
        var result = Money.Create(10.555m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(10.56m);
    }

    [Fact]
    public void Add_SameCurrency_SumsAmounts()
    {
        var a = Money.From(100m);
        var b = Money.From(49.50m);

        (a + b).Amount.Should().Be(149.50m);
    }

    [Fact]
    public void Add_DifferentCurrency_Throws()
    {
        var inr = Money.From(100m, "INR");
        var usd = Money.From(100m, "USD");

        var act = () => _ = inr + usd;

        act.Should().Throw<InvalidOperationException>().WithMessage("*Currency mismatch*");
    }

    [Fact]
    public void Multiply_ByQuantity_ScalesAmount()
    {
        var price = Money.From(199.99m);

        (price * 3).Amount.Should().Be(599.97m);
    }

    [Fact]
    public void Comparison_Operators_WorkWithinSameCurrency()
    {
        var low = Money.From(50m);
        var high = Money.From(500m);

        (low < high).Should().BeTrue();
        (high > low).Should().BeTrue();
        (low <= Money.From(50m)).Should().BeTrue();
    }
}
