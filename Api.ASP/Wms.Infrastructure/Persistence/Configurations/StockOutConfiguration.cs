using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class StockOutConfiguration : EntityConfiguration<StockOut>
{
    public override void Configure(EntityTypeBuilder<StockOut> builder)
    {
        base.Configure(builder);

        builder.ToTable("StockOuts");

        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey("StockOutId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
