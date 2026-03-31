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

        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey("StockInId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
