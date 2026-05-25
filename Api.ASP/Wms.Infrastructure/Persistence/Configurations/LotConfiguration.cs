using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wms.Domain.Entities;

namespace Wms.Infrastructure.Persistence.Configurations;

public class LotConfiguration : EntityConfiguration<Lot>
{
    public override void Configure(EntityTypeBuilder<Lot> builder)
    {
        base.Configure(builder);

        builder.ToTable("Lots");

        builder.OwnsOne(l => l.Number, numberBuilder =>
        {
            numberBuilder.Property(n => n.Value)
                .HasColumnName("Number")
                .HasMaxLength(100)
                .IsRequired();

            numberBuilder.HasIndex(n => n.Value);
        });

        builder.Property(l => l.ProductId).IsRequired();

        builder.Property(l => l.ManufactureDate).IsRequired(false);
        builder.Property(l => l.ExpirationDate).IsRequired(false);

        builder.HasIndex(l => l.ExpirationDate);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
