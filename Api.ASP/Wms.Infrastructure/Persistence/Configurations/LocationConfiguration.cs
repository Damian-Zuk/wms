
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class LocationConfiguration : EntityConfiguration<Location>
{
    public override void Configure(EntityTypeBuilder<Location> builder)
    {
        base.Configure(builder);

        builder.ToTable("Locations");

        builder.OwnsOne(l => l.Code, codeBuilder =>
        {
            codeBuilder.Property(c => c.Value)
                .HasColumnName("Code")
                .HasMaxLength(50)
                .IsRequired();

            codeBuilder.HasIndex(c => c.Value)
                .IsUnique();
        });

        builder.OwnsOne(l => l.Address, addressBuilder =>
        {
            addressBuilder.Property(a => a.Zone)
                .HasColumnName("AddressZone")
                .HasMaxLength(8)
                .IsRequired();

            addressBuilder.Property(a => a.Aisle)
                .HasColumnName("AddressAisle")
                .HasMaxLength(8)
                .IsRequired();

            addressBuilder.Property(a => a.Rack)
                .HasColumnName("AddressRack")
                .HasMaxLength(8)
                .IsRequired();

            addressBuilder.Property(a => a.Shelf)
                .HasColumnName("AddressShelf")
                .HasMaxLength(8)
                .IsRequired();

            addressBuilder.Property(a => a.Bin)
                .HasColumnName("AddressBin")
                .HasMaxLength(8)
                .IsRequired();

            addressBuilder.HasIndex(a => new { a.Zone, a.Aisle, a.Rack, a.Shelf, a.Bin })
                .IsUnique();
        });

        builder.Property(l => l.Description)
            .HasMaxLength(500);

        builder.Property(l => l.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(l => l.TemperatureZone)
            .HasConversion<int>()
            .IsRequired();

        builder.OwnsOne(l => l.Capacity, capacityBuilder =>
        {
            capacityBuilder.Property(c => c.MaxUnits)
                .HasColumnName("CapacityUnits");

            capacityBuilder.Property(c => c.MaxWeight)
                .HasColumnName("CapacityWeight")
                .HasPrecision(18, 3);

            capacityBuilder.Property(c => c.MaxVolume)
                .HasColumnName("CapacityVolume")
                .HasPrecision(18, 3);
        });

        builder.Navigation(l => l.Capacity).IsRequired();

        builder.Property(l => l.IsMixedSkuAllowed)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(l => l.IsMixedLotAllowed)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(l => l.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(l => l.IsBlocked)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(l => l.BlockedReason)
            .HasMaxLength(500);
    }
}
