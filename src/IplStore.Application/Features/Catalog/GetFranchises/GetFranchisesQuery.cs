using IplStore.Application.Common;
using IplStore.Application.Common.Abstractions;
using IplStore.Application.Common.Behaviors;
using IplStore.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Catalog.GetFranchises;

public sealed record GetFranchisesQuery
    : IRequest<Result<IReadOnlyList<FranchiseDto>>>, ICacheableQuery
{
    public string CacheKey => CacheKeys.FranchiseList();
    public TimeSpan? CacheDuration => TimeSpan.FromMinutes(10);
}

public sealed class GetFranchisesQueryHandler
    : IRequestHandler<GetFranchisesQuery, Result<IReadOnlyList<FranchiseDto>>>
{
    private readonly IAppDbContext _db;

    public GetFranchisesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<FranchiseDto>>> Handle(
        GetFranchisesQuery request, CancellationToken cancellationToken)
    {
        var franchises = await _db.Franchises
            .AsNoTracking()
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken);

        IReadOnlyList<FranchiseDto> dtos = franchises.Select(f => f.ToDto()).ToList();
        return Result.Success(dtos);
    }
}
