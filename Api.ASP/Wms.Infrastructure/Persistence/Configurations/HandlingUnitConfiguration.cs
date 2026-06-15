using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class HandlingUnitConfiguration : EntityConfiguration<HandlingUnit>
{
    public override void Configure(EntityTypeBuilder<HandlingUnit> builder)
    {
        base.Configure(builder);

        builder.ToTable("HandlingUnits");

        builder.OwnsOne(h => h.Code, codeBuilder =>
        {
            codeBuilder.Property(c => c.Value)
                .HasColumnName("Code")
                .HasMaxLength(50)
                .IsRequired();

            codeBuilder.HasIndex(c => c.Value)
                .IsUnique().HasFilter("\"IsDeleted\" = false");
        });

        builder.Property(h => h.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(h => h.LocationId)
            .IsRequired(false);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(h => h.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => h.LocationId);
    }
}
