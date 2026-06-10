using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : EntityConfiguration<Product>
{
    public override void Configure(EntityTypeBuilder<Product> builder)
    {
        base.Configure(builder);

        builder.ToTable("Products");

        builder.OwnsOne(p => p.Sku, skuBuilder =>
        {
            skuBuilder.Property(s => s.Value)
                .HasColumnName("Sku")
                .HasMaxLength(100)
                .IsRequired();

            skuBuilder.HasIndex(s => s.Value)
                .IsUnique().HasFilter("\"IsDeleted\" = false");
        });

        builder.Property(p => p.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(p => p.Weight)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(p => p.Volume)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(p => p.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.RequiredTemperatureZone)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne<ProductCategory>()
            .WithMany()
            .HasForeignKey(p => p.ProductCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.ProductCategoryId);

        builder.HasMany(p => p.PreferredLocations)
            .WithOne()
            .HasForeignKey(pl => pl.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.PreferredLocations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
