using IplStore.Domain.Enums;

namespace IplStore.Application.Common.Abstractions;

public interface IJwtTokenService
{
    AuthTokens GenerateTokens(Guid userId, string email, IEnumerable<string> roles);
    string GenerateRefreshToken();
}

public sealed record AuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    DateTime RefreshTokenExpiresAtUtc);

/// <summary>Boundary to the (mock) payment provider. Swappable for Stripe/Razorpay in production.</summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default);
}

public sealed record PaymentRequest(
    Guid OrderId,
    string OrderNumber,
    decimal Amount,
    string Currency,
    PaymentMethod Method);

public sealed record PaymentResult(bool Success, string? TransactionId, string? FailureReason)
{
    public static PaymentResult Ok(string txnId) => new(true, txnId, null);
    public static PaymentResult Declined(string reason) => new(false, null, reason);
}

public interface IOrderNumberGenerator
{
    string Next();
}
