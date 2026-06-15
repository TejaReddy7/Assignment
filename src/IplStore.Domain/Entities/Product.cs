using IplStore.Domain.Enums;
using IplStore.Domain.Errors;
using IplStore.Domain.Events;
using IplStore.Domain.Primitives;
using IplStore.Domain.ValueObjects;
using IplStore.Shared;

namespace IplStore.Domain.Entities;

public sealed class Product : Entity<Guid>, IAggregateRoot, IAuditable, ISoftDeletable
{
    private readonly List<ProductVariant> _variants = new();
    private readonly List<Review> _reviews = new();

    private Product() { } // EF

    private Product(
        Guid id,
        string name,
        Slug slug,
        string description,
        ProductType type,
        Guid franchiseId,
        Money basePrice,
        string? imageUrl)
        : base(id)
    {
        Name = name;
        Slug = slug;
        Description = description;
        Type = type;
        FranchiseId = franchiseId;
        BasePrice = basePrice;
        ImageUrl = imageUrl;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public string Name { get; private set; } = default!;
    public Slug Slug { get; private set; }
    public string Description { get; private set; } = default!;
    public ProductType Type { get; private set; }
    public Guid FranchiseId { get; private set; }
    public Franchise Franchise { get; private set; } = default!;
    public Money BasePrice { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsActive { get; private set; }

    // Denormalized for read perf; recomputed via review domain events.
    public decimal AverageRating { get; private set; }
    public int ReviewCount { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();
    public IReadOnlyCollection<Review> Reviews => _reviews.AsReadOnly();

    public static Result<Product> Create(
        string name, string description, ProductType type, Guid franchiseId,
        Money basePrice, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(name)) return Error.Validation("product.name_required", "Name is required.");
        if (name.Length > 200) return Error.Validation("product.name_too_long", "Name cannot exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(description)) return Error.Validation("product.desc_required", "Description is required.");
        if (basePrice.Amount <= 0) return Error.Validation("product.price_invalid", "Base price must be greater than zero.");

        var slugResult = Slug.Create(name);
        if (slugResult.IsFailure) return slugResult.Error;

        return new Product(Guid.NewGuid(), name.Trim(), slugResult.Value, description.Trim(),
            type, franchiseId, basePrice, imageUrl);
    }

    public Result Update(string name, string description, Money basePrice, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(name)) return Result.Failure(Error.Validation("product.name_required", "Name is required."));
        if (basePrice.Amount <= 0) return Result.Failure(Error.Validation("product.price_invalid", "Base price must be greater than zero."));

        Name = name.Trim();
        Description = description.Trim();
        BasePrice = basePrice;
        ImageUrl = imageUrl;
        UpdatedAtUtc = DateTime.UtcNow;
        return Result.Success();
    }

    public Result<ProductVariant> AddVariant(string sku, string? size, string? color, int initialStock, Money? priceOverride = null)
    {
        if (_variants.Any(v => string.Equals(v.Sku, sku, StringComparison.OrdinalIgnoreCase)))
            return DomainErrors.Variant.SkuTaken(sku);

        var variantResult = ProductVariant.Create(Id, sku, size, color, initialStock, priceOverride);
        if (variantResult.IsFailure) return variantResult.Error;

        _variants.Add(variantResult.Value);
        UpdatedAtUtc = DateTime.UtcNow;
        return variantResult.Value;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        IsActive = false;
        DeletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Recomputes the denormalized rating aggregate. Called by the review-added/removed domain event handler.
    /// </summary>
    public void RecomputeRating(decimal averageRating, int reviewCount)
    {
        AverageRating = decimal.Round(averageRating, 2, MidpointRounding.AwayFromZero);
        ReviewCount = reviewCount;
        UpdatedAtUtc = DateTime.UtcNow;
        RaiseDomainEvent(new ProductRatingUpdatedEvent(Id, AverageRating, ReviewCount));
    }
}
