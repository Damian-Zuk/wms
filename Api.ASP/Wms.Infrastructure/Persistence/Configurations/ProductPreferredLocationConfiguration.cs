using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class ProductPreferredLocationConfiguration : IEntityTypeConfiguration<ProductPreferredLocation>
{
    public void Configure(EntityTypeBuilder<ProductPreferredLocation> builder)
    {
        builder.ToTable("ProductPreferredLocations");

        builder.HasKey(p => new { p.ProductId, p.LocationId });

        builder.Property(p => p.ProductId);
        builder.Property(p => p.LocationId);
        builder.Property(p => p.Sequence).IsRequired();

        builder.HasIndex(p => p.LocationId);
        builder.HasIndex(p => new { p.ProductId, p.Sequence });

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(p => p.LocationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
