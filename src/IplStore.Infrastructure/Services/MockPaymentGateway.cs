using IplStore.Application.Common.Abstractions;
using Microsoft.Extensions.Logging;

namespace IplStore.Infrastructure.Services;

/// <summary>
/// Mock payment gateway. Approves every charge except amounts ending in .13
/// (a deterministic hook so tests/demos can exercise the decline path).
/// Behind IPaymentGateway so swapping in Razorpay/Stripe is a one-line DI change.
/// </summary>
public sealed class MockPaymentGateway : IPaymentGateway
{
    private readonly ILogger<MockPaymentGateway> _logger;

    public MockPaymentGateway(ILogger<MockPaymentGateway> logger) => _logger = logger;

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default)
    {
        // Deterministic decline trigger for demos: amount with paise == .13
        var paise = (int)Math.Round((request.Amount - Math.Truncate(request.Amount)) * 100m);
        if (paise == 13)
        {
            _logger.LogWarning("Payment declined for order {OrderNumber} (demo decline trigger).", request.OrderNumber);
            return Task.FromResult(PaymentResult.Declined("Card declined by issuer."));
        }

        var txnId = $"txn_{Guid.NewGuid():N}";
        _logger.LogInformation("Payment captured {TxnId} for order {OrderNumber}, amount {Amount} {Currency}.",
            txnId, request.OrderNumber, request.Amount, request.Currency);
        return Task.FromResult(PaymentResult.Ok(txnId));
    }
}
