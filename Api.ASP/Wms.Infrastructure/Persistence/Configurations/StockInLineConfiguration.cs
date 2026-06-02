using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class StockInLineConfiguration : EntityConfiguration<StockInLine>
{
    public override void Configure(EntityTypeBuilder<StockInLine> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockInLines");

        builder.Property<Guid>("StockInId")
            .IsRequired();

        builder.ComplexProperty(l => l.Quantity, qtyBuilder =>
        {
            qtyBuilder.Property(q => q.Value)
                .HasColumnName("Quantity")
                .IsRequired();
        });

        builder.Property(l => l.LotId)
            .IsRequired(false);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Lot>()
            .WithMany()
            .HasForeignKey(l => l.LotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Items)
            .WithOne()
            .HasForeignKey("StockInLineId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(l => l.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex("StockInId");
    }
}
