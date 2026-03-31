
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

        builder.Property(l => l.Description)
            .HasMaxLength(500);
    }
}
