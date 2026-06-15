using IplStore.Application.Common.Abstractions;
using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Application.Features.Reviews;

/// <summary>
/// Recomputes a product's denormalized AverageRating + ReviewCount from its reviews.
/// Pass an optional delta review (just-created, not yet saved) so the aggregate reflects it.
/// </summary>
public static class RatingRecalculator
{
    public static async Task RecomputeAsync(
        IAppDbContext db, Product product, Review? pendingAddition, Guid? pendingRemovalId, CancellationToken ct)
    {
        var ratings = await db.Reviews
            .Where(r => r.ProductId == product.Id)
            .Select(r => new { r.Id, r.Rating })
            .ToListAsync(ct);

        var effective = ratings
            .Where(r => r.Id != pendingRemovalId)
            .Select(r => r.Rating)
            .ToList();

        if (pendingAddition is not null)
            effective.Add(pendingAddition.Rating);

        var count = effective.Count;
        var average = count == 0 ? 0m : (decimal)effective.Average();
        product.RecomputeRating(average, count);
    }
}
