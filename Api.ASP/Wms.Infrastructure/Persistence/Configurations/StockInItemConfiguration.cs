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

        builder.Property<Guid>("StockInId")
            .IsRequired();

        builder.ComplexProperty(i => i.Quantity, qtyBuilder =>
        {
            qtyBuilder.Property(q => q.Value)
                .HasColumnName("Quantity")
                .IsRequired();
        });

        builder.Property(i => i.LotId)
            .IsRequired(false);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(i => i.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Lot>()
            .WithMany()
            .HasForeignKey(i => i.LotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
