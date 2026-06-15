using IplStore.Domain.Entities;
using IplStore.Domain.Enums;
using IplStore.Domain.ValueObjects;
using IplStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IplStore.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the catalog with real IPL franchises and representative merchandise so the
/// app is demo-ready on first run. Idempotent: skips if franchises already exist.
/// </summary>
public static class CatalogSeeder
{
    private sealed record FranchiseSpec(string Name, string Code, string City, string Color, int Year);

    private static readonly FranchiseSpec[] Franchises =
    {
        new("Mumbai Indians", "MI", "Mumbai", "#004BA0", 2008),
        new("Chennai Super Kings", "CSK", "Chennai", "#FDB913", 2008),
        new("Royal Challengers Bengaluru", "RCB", "Bengaluru", "#EC1C24", 2008),
        new("Kolkata Knight Riders", "KKR", "Kolkata", "#3A225D", 2008),
        new("Sunrisers Hyderabad", "SRH", "Hyderabad", "#FB643E", 2013),
        new("Delhi Capitals", "DC", "Delhi", "#17449B", 2008),
        new("Rajasthan Royals", "RR", "Jaipur", "#EA1A85", 2008),
        new("Punjab Kings", "PBKS", "Mohali", "#DD1F2D", 2008),
        new("Gujarat Titans", "GT", "Ahmedabad", "#1B2133", 2022),
        new("Lucknow Super Giants", "LSG", "Lucknow", "#A7D5F2", 2022),
    };

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Franchises.AnyAsync(ct)) return;

        var franchises = new Dictionary<string, Franchise>();
        foreach (var spec in Franchises)
        {
            var f = Franchise.Create(spec.Name, spec.Code, spec.City, spec.Color, spec.Year,
                $"https://cdn.iplstore.local/logos/{spec.Code.ToLowerInvariant()}.png").Value;
            franchises[spec.Code] = f;
            db.Franchises.Add(f);
        }

        var rng = new Random(42);
        foreach (var (code, franchise) in franchises)
        {
            AddProduct(db, franchise, $"{franchise.Name} Home Jersey 2026", ProductType.Jersey, 1999m, rng,
                sizes: new[] { "S", "M", "L", "XL", "XXL" }, color: "Home");
            AddProduct(db, franchise, $"{franchise.Name} Away Jersey 2026", ProductType.Jersey, 1999m, rng,
                sizes: new[] { "S", "M", "L", "XL" }, color: "Away");
            AddProduct(db, franchise, $"{franchise.Name} Official Cap", ProductType.Cap, 699m, rng,
                sizes: new[] { "Free" }, color: "Team");
            AddProduct(db, franchise, $"{franchise.Name} Team Flag", ProductType.Flag, 499m, rng,
                sizes: new[] { "Standard" }, color: "Team");
            AddProduct(db, franchise, $"{franchise.Name} Captain Autographed Photo", ProductType.AutographedPhoto, 2999m, rng,
                sizes: new[] { "A4" }, color: "Framed", limitedStock: true);
        }

        await db.SaveChangesAsync(ct);
    }

    private static void AddProduct(
        AppDbContext db, Franchise franchise, string name, ProductType type, decimal price,
        Random rng, string[] sizes, string color, bool limitedStock = false)
    {
        var description = type switch
        {
            ProductType.Jersey => $"Official {franchise.Name} {color.ToLowerInvariant()} jersey for the 2026 season. Breathable fabric, authentic team crest.",
            ProductType.Cap => $"Premium {franchise.Name} cap with embroidered logo. One size fits most.",
            ProductType.Flag => $"Wave your colours with the official {franchise.Name} flag. Weather-resistant print.",
            ProductType.AutographedPhoto => $"Limited-edition autographed photo of the {franchise.Name} captain. Certificate of authenticity included.",
            _ => $"{franchise.Name} official merchandise."
        };

        var product = Product.Create(name, description, type, franchise.Id,
            Money.From(price), $"https://cdn.iplstore.local/products/{franchise.ShortCode.ToLowerInvariant()}-{type.ToString().ToLowerInvariant()}.png").Value;

        var skuType = type.ToString()[..3].ToUpperInvariant();
        foreach (var size in sizes)
        {
            var sku = $"{franchise.ShortCode}-{skuType}-{size}-{color}".ToUpperInvariant().Replace(" ", "");
            var stock = limitedStock ? rng.Next(2, 10) : rng.Next(20, 200);
            product.AddVariant(sku, size == "Free" || size == "Standard" || size == "A4" ? null : size, color, stock);
        }

        db.Products.Add(product);
    }
}
