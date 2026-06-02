using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class CapacityReservationConfiguration : EntityConfiguration<CapacityReservation>
{
    public override void Configure(EntityTypeBuilder<CapacityReservation> builder)
    {
        base.Configure(builder);

        builder.ToTable("CapacityReservations");

        builder.ComplexProperty(r => r.Quantity, qtyBuilder =>
        {
            qtyBuilder.Property(q => q.Value)
                .HasColumnName("Quantity")
                .IsRequired();
        });

        builder.Property(r => r.LotId)
            .IsRequired(false);

        builder.HasOne<StockIn>()
            .WithMany()
            .HasForeignKey(r => r.StockInId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(r => r.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Lot>()
            .WithMany()
            .HasForeignKey(r => r.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.LocationId);
        builder.HasIndex(r => r.StockInId);
        builder.HasIndex(r => r.StockInItemId).IsUnique();
    }
}
