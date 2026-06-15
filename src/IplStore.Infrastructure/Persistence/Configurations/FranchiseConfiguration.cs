using IplStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IplStore.Infrastructure.Persistence.Configurations;

public sealed class FranchiseConfiguration : IEntityTypeConfiguration<Franchise>
{
    public void Configure(EntityTypeBuilder<Franchise> builder)
    {
        builder.ToTable("Franchises");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).HasMaxLength(120).IsRequired();
        builder.Property(f => f.ShortCode).HasMaxLength(5).IsRequired();
        builder.Property(f => f.City).HasMaxLength(80).IsRequired();
        builder.Property(f => f.PrimaryColor).HasMaxLength(20).IsRequired();
        builder.Property(f => f.LogoUrl).HasMaxLength(500);

        builder.HasIndex(f => f.ShortCode).IsUnique();

        builder.HasMany(f => f.Products)
            .WithOne(p => p.Franchise)
            .HasForeignKey(p => p.FranchiseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
