using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class StockOutItemConfiguration : EntityConfiguration<StockOutItem>
{
    public override void Configure(EntityTypeBuilder<StockOutItem> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockOutItems");

        builder.Property<Guid>("StockOutLineId")
            .IsRequired();

        builder.ComplexProperty(i => i.Quantity, qtyBuilder =>
        {
            qtyBuilder.Property(q => q.Value)
                .HasColumnName("Quantity")
                .IsRequired();
        });

        builder.ComplexProperty(i => i.PickedQuantity, qtyBuilder =>
        {
            qtyBuilder.Property(q => q.Value)
                .HasColumnName("PickedQuantity")
                .HasDefaultValue(0)
                .IsRequired();
        });

        builder.Property(i => i.Strategy)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.LotId)
            .IsRequired(false);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(i => i.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Lot>()
            .WithMany()
            .HasForeignKey(i => i.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex("StockOutLineId");
        builder.HasIndex(i => i.LocationId);
    }
}
