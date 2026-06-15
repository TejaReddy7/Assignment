using IplStore.Application.Common.Abstractions;
using IplStore.Application.Common.Messaging;
using IplStore.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IplStore.Application.Features.Orders.EventHandlers;

/// <summary>
/// When an order is cancelled, return the reserved stock to each variant.
/// Subscribes to OrderCancelledEvent raised by Order.Cancel().
/// </summary>
public sealed class RestockOnOrderCancelledHandler : DomainEventHandler<OrderCancelledEvent>
{
    private readonly IAppDbContext _db;
    private readonly ILogger<RestockOnOrderCancelledHandler> _logger;

    public RestockOnOrderCancelledHandler(IAppDbContext db, ILogger<RestockOnOrderCancelledHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    protected override async Task HandleEvent(OrderCancelledEvent domainEvent, CancellationToken cancellationToken)
    {
        var variantIds = domainEvent.ItemsToRestock.Select(i => i.VariantId).ToList();
        var variants = await _db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        foreach (var (variantId, quantity) in domainEvent.ItemsToRestock)
        {
            if (variants.TryGetValue(variantId, out var variant))
                variant.Restock(quantity);
        }

        // No SaveChanges here: this handler runs inside the triggering SaveChangesAsync,
        // so the restock mutations are persisted atomically with the cancellation.
        _logger.LogInformation("Restocked {Count} variants after cancelling order {OrderNumber}.",
            domainEvent.ItemsToRestock.Count, domainEvent.OrderNumber);
    }
}
