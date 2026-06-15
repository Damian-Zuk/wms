using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class InventoryConfiguration : EntityConfiguration<Inventory>
{
    public override void Configure(EntityTypeBuilder<Inventory> builder)
    {
        base.Configure(builder);

        builder.ToTable("Inventories");

        builder.HasIndex(i => new { i.ProductId, i.LocationId, i.LotId, i.HandlingUnitId })
            .IsUnique();

        builder.OwnsOne(i => i.OnHand, qtyBuilder =>
        {
            qtyBuilder.Property(q => q.Value)
                .HasColumnName("OnHand")
                .IsRequired();
        });

        builder.OwnsOne(i => i.Reserved, qtyBuilder =>
        {
            qtyBuilder.Property(q => q.Value)
                .HasColumnName("Reserved")
                .IsRequired();
        });

        builder.Ignore(i => i.Available);

        builder.Property(i => i.LotId).IsRequired(false);

        builder.Property(i => i.ReceivedAt).IsRequired(false);

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

        builder.Property(i => i.HandlingUnitId).IsRequired(false);

        builder.HasOne<HandlingUnit>()
            .WithMany()
            .HasForeignKey(i => i.HandlingUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.HandlingUnitId);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
