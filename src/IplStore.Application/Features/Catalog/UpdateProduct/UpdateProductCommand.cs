using FluentValidation;
using IplStore.Application.Common;
using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Domain.ValueObjects;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Catalog.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string Description,
    decimal BasePrice,
    string? ImageUrl) : IRequest<Result>;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.BasePrice).GreaterThan(0);
    }
}

public sealed class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result>
{
    private readonly IAppDbContext _db;
    private readonly ICacheService _cache;

    public UpdateProductCommandHandler(IAppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (product is null) return Result.Failure(DomainErrors.Product.NotFound);

        var update = product.Update(request.Name, request.Description, Money.From(request.BasePrice), request.ImageUrl);
        if (update.IsFailure) return update;

        await _db.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByPrefixAsync(CacheKeys.ProductPrefix, cancellationToken);
        return Result.Success();
    }
}
