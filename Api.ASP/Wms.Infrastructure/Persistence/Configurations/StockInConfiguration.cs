using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class StockInConfiguration : EntityConfiguration<StockIn>
{
    public override void Configure(EntityTypeBuilder<StockIn> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockIns");

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.CancelledFrom)
            .HasConversion<int?>();

        builder.Property(s => s.Description)
            .HasMaxLength(500);

        builder.Property(s => s.ModifiedBy)
            .HasMaxLength(256);

        builder.HasMany(s => s.Lines)
            .WithOne()
            .HasForeignKey("StockInId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
