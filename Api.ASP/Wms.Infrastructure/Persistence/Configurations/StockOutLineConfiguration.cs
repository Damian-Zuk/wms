using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class StockOutLineConfiguration : EntityConfiguration<StockOutLine>
{
    public override void Configure(EntityTypeBuilder<StockOutLine> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockOutLines");

        builder.Property<Guid>("StockOutId")
            .IsRequired();

        builder.ComplexProperty(l => l.Quantity, qtyBuilder =>
        {
            qtyBuilder.Property(q => q.Value)
                .HasColumnName("Quantity")
                .IsRequired();
        });

        builder.Property(l => l.Strategy)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Items)
            .WithOne()
            .HasForeignKey("StockOutLineId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(l => l.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex("StockOutId");
    }
}
