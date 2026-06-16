using FluentValidation;
using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Cart.MergeCart;

public sealed record MergeCartLine(Guid ProductVariantId, int Quantity);

/// <summary>
/// Merges a guest cart (built client-side in localStorage) into the authenticated
/// user's server cart in a single atomic request. Used right after login so a
/// browser doesn't have to fire N concurrent add-item calls (which would race).
/// </summary>
public sealed record MergeCartCommand(IReadOnlyList<MergeCartLine> Items) : IRequest<Result<CartDto>>;

public sealed class MergeCartCommandValidator : AbstractValidator<MergeCartCommand>
{
    public MergeCartCommandValidator()
    {
        RuleForEach(x => x.Items).ChildRules(line =>
        {
            line.RuleFor(i => i.ProductVariantId).NotEmpty();
            line.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}

public sealed class MergeCartCommandHandler : IRequestHandler<MergeCartCommand, Result<CartDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public MergeCartCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<CartDto>> Handle(MergeCartCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } customerId)
            return DomainErrors.Auth.InvalidCredentials;

        var cart = await CartLoader.GetOrCreateAsync(_db, customerId, cancellationToken);

        if (request.Items.Count == 0)
            return cart.ToDto();

        var variantIds = request.Items.Select(i => i.ProductVariantId).Distinct().ToList();
        // Read-only reference data — load untracked so EF never marks the variant Modified.
        var variants = await _db.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var existingItemIds = cart.Items.Select(i => i.Id).ToHashSet();

        foreach (var line in request.Items)
        {
            if (!variants.TryGetValue(line.ProductVariantId, out var variant)) continue;
            if (!variant.Product.IsActive) continue;

            var unitPrice = variant.EffectivePrice(variant.Product.BasePrice);
            // Best-effort: skip lines that exceed stock/limits rather than failing the whole merge.
            var add = cart.AddOrMerge(
                variant.ProductId,
                variant.Id,
                variant.Product.Name,
                variant.Product.ImageUrl,
                unitPrice,
                line.Quantity,
                variant.StockQuantity);

            // Explicitly mark brand-new lines as Added (client-generated GUID keys — see AddCartItem).
            if (add.IsSuccess && !existingItemIds.Contains(add.Value.Id))
            {
                _db.CartItems.Add(add.Value);
                existingItemIds.Add(add.Value.Id);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return cart.ToDto();
    }
}
