using IplStore.Domain.Primitives;
using IplStore.Shared;

namespace IplStore.Domain.Entities;

public sealed class Franchise : Entity<Guid>, IAggregateRoot, IAuditable
{
    private readonly List<Product> _products = new();

    private Franchise() { } // EF

    private Franchise(Guid id, string name, string shortCode, string city, string primaryColor, int foundedYear)
        : base(id)
    {
        Name = name;
        ShortCode = shortCode;
        City = city;
        PrimaryColor = primaryColor;
        FoundedYear = foundedYear;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public string Name { get; private set; } = default!;
    public string ShortCode { get; private set; } = default!;
    public string City { get; private set; } = default!;
    public string PrimaryColor { get; private set; } = default!;
    public int FoundedYear { get; private set; }
    public string? LogoUrl { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<Product> Products => _products.AsReadOnly();

    public static Result<Franchise> Create(string name, string shortCode, string city, string primaryColor, int foundedYear, string? logoUrl = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return Error.Validation("franchise.name_required", "Name is required.");
        if (string.IsNullOrWhiteSpace(shortCode) || shortCode.Length is < 2 or > 5)
            return Error.Validation("franchise.code_invalid", "Short code must be 2-5 characters.");
        if (foundedYear < 2007 || foundedYear > DateTime.UtcNow.Year)
            return Error.Validation("franchise.year_invalid", "Founded year is not valid.");

        var f = new Franchise(Guid.NewGuid(), name.Trim(), shortCode.Trim().ToUpperInvariant(),
            city.Trim(), primaryColor.Trim(), foundedYear);
        f.LogoUrl = logoUrl;
        return f;
    }

    public void UpdateLogo(string? logoUrl)
    {
        LogoUrl = logoUrl;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
