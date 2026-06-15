using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : EntityConfiguration<StockMovement>
{
    public override void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockMovements");

        builder.HasIndex(m => new { m.ProductId, m.LocationId, m.CreatedAt });
        builder.HasIndex(m => new { m.SourceId, m.Source });

        builder.Property(m => m.QuantityChange)
            .IsRequired();

        builder.Property(m => m.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Source)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.SourceId)
            .IsRequired();

        builder.Property(m => m.LotId)
            .IsRequired(false);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(m => m.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Lot>()
            .WithMany()
            .HasForeignKey(m => m.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.HandlingUnitId)
            .IsRequired(false);

        builder.HasOne<HandlingUnit>()
            .WithMany()
            .HasForeignKey(m => m.HandlingUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.HandlingUnitId);
    }
}
