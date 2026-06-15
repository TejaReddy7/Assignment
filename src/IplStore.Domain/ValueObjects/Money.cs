using IplStore.Shared;

namespace IplStore.Domain.ValueObjects;

/// <summary>
/// Immutable monetary value with currency. Guards against accidental decimal/int math and
/// cross-currency arithmetic (a real bug class in payments code).
/// </summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    public const string DefaultCurrency = "INR";
    public static readonly Money Zero = new(0m, DefaultCurrency);

    public static Result<Money> Create(decimal amount, string currency = DefaultCurrency)
    {
        if (amount < 0) return Error.Validation("money.negative", "Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            return Error.Validation("money.currency_invalid", "Currency must be a 3-letter ISO code.");
        return new Money(decimal.Round(amount, 2, MidpointRounding.AwayFromZero), currency.ToUpperInvariant());
    }

    public static Money From(decimal amount, string currency = DefaultCurrency) =>
        Create(amount, currency).Value;

    public Money Add(Money other) => SameCurrency(other) with { Amount = Amount + other.Amount };
    public Money Subtract(Money other) => SameCurrency(other) with { Amount = Amount - other.Amount };
    public Money Multiply(int multiplier) => this with { Amount = decimal.Round(Amount * multiplier, 2, MidpointRounding.AwayFromZero) };
    public Money Multiply(decimal multiplier) => this with { Amount = decimal.Round(Amount * multiplier, 2, MidpointRounding.AwayFromZero) };

    public static Money operator +(Money a, Money b) => a.Add(b);
    public static Money operator -(Money a, Money b) => a.Subtract(b);
    public static Money operator *(Money a, int n) => a.Multiply(n);
    public static Money operator *(Money a, decimal d) => a.Multiply(d);

    public static bool operator <(Money a, Money b) => a.SameCurrency(b).Amount < b.Amount;
    public static bool operator >(Money a, Money b) => a.SameCurrency(b).Amount > b.Amount;
    public static bool operator <=(Money a, Money b) => a.SameCurrency(b).Amount <= b.Amount;
    public static bool operator >=(Money a, Money b) => a.SameCurrency(b).Amount >= b.Amount;

    private Money SameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
            throw new InvalidOperationException($"Currency mismatch: {Currency} vs {other.Currency}.");
        return this;
    }

    public override string ToString() => $"{Currency} {Amount:0.00}";
}
