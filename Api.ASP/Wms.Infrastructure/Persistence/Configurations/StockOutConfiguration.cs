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

        builder.Property(s => s.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.CancelledFrom)
            .HasConversion<int?>();

        builder.Property(s => s.Description)
            .HasMaxLength(500);

        builder.HasMany(s => s.Lines)
            .WithOne()
            .HasForeignKey("StockOutId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
