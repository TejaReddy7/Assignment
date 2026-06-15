using FluentValidation;
using IplStore.Application.Common;
using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Entities;
using IplStore.Domain.Enums;
using IplStore.Domain.Errors;
using IplStore.Domain.ValueObjects;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Catalog.CreateProduct;

public sealed record CreateProductVariantInput(string Sku, string? Size, string? Color, int Stock, decimal? PriceOverride);

public sealed record CreateProductCommand(
    string Name,
    string Description,
    ProductType Type,
    Guid FranchiseId,
    decimal BasePrice,
    string? ImageUrl,
    IReadOnlyList<CreateProductVariantInput> Variants)
    : IRequest<Result<ProductDetailsDto>>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.FranchiseId).NotEmpty();
        RuleFor(x => x.BasePrice).GreaterThan(0);
        RuleForEach(x => x.Variants).ChildRules(v =>
        {
            v.RuleFor(i => i.Sku).NotEmpty().MaximumLength(64);
            v.RuleFor(i => i.Stock).GreaterThanOrEqualTo(0);
            v.RuleFor(i => i.PriceOverride).GreaterThan(0).When(i => i.PriceOverride.HasValue);
        });
    }
}

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<ProductDetailsDto>>
{
    private readonly IAppDbContext _db;
    private readonly ICacheService _cache;

    public CreateProductCommandHandler(IAppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<ProductDetailsDto>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var franchise = await _db.Franchises.FindAsync([request.FranchiseId], cancellationToken);
        if (franchise is null) return DomainErrors.Franchise.NotFound;

        var basePrice = Money.From(request.BasePrice);
        var productResult = Product.Create(request.Name, request.Description, request.Type,
            request.FranchiseId, basePrice, request.ImageUrl);
        if (productResult.IsFailure) return productResult.Error;

        var product = productResult.Value;

        // Ensure slug uniqueness.
        var slugExists = await _db.Products.AnyAsync(p => p.Slug == product.Slug, cancellationToken);
        if (slugExists) return DomainErrors.Product.SlugTaken;

        foreach (var v in request.Variants)
        {
            var priceOverride = v.PriceOverride.HasValue ? Money.From(v.PriceOverride.Value) : (Money?)null;
            var added = product.AddVariant(v.Sku, v.Size, v.Color, v.Stock, priceOverride);
            if (added.IsFailure) return added.Error;
        }

        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByPrefixAsync(CacheKeys.ProductPrefix, cancellationToken);

        // franchise is tracked and product.FranchiseId matches, so EF relationship fixup
        // has populated product.Franchise — safe to project to the details DTO.
        return product.ToDetailsDto();
    }
}
