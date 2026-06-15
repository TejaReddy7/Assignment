using IplStore.Application.Common;
using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Errors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Catalog.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, Result>
{
    private readonly IAppDbContext _db;
    private readonly ICacheService _cache;

    public DeleteProductCommandHandler(IAppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (product is null) return Result.Failure(DomainErrors.Product.NotFound);

        product.SoftDelete(); // soft delete — preserves order history references
        await _db.SaveChangesAsync(cancellationToken);
        await _cache.RemoveByPrefixAsync(CacheKeys.ProductPrefix, cancellationToken);
        return Result.Success();
    }
}
