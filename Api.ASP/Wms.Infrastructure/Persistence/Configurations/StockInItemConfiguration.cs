using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class StockInItemConfiguration : EntityConfiguration<StockInItem>
{
    public override void Configure(EntityTypeBuilder<StockInItem> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockInItems");

        builder.Property<Guid>("StockInLineId")
            .IsRequired();

        builder.ComplexProperty(i => i.Quantity, qtyBuilder =>
        {
            qtyBuilder.Property(q => q.Value)
                .HasColumnName("Quantity")
                .IsRequired();
        });

        builder.ComplexProperty(i => i.PlacedQuantity, qtyBuilder =>
        {
            qtyBuilder.Property(q => q.Value)
                .HasColumnName("PlacedQuantity")
                .HasDefaultValue(0)
                .IsRequired();
        });

        builder.Property(i => i.Strategy)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(i => i.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(i => i.HandlingUnitId)
            .IsRequired(false);

        builder.HasOne<HandlingUnit>()
            .WithMany()
            .HasForeignKey(i => i.HandlingUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex("StockInLineId");
        builder.HasIndex(i => i.LocationId);
    }
}
