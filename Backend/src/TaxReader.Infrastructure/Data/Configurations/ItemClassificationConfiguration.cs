using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Data.Configurations;

public class ItemClassificationConfiguration : IEntityTypeConfiguration<ItemClassification>
{
    public void Configure(EntityTypeBuilder<ItemClassification> builder)
    {
        builder.ToTable("item_classifications");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.Category).HasConversion<string>().IsRequired().HasMaxLength(100);
        builder.Property(e => e.Method).HasConversion<string>().IsRequired().HasMaxLength(50);
        builder.Property(e => e.Status).HasConversion<string>().IsRequired().HasMaxLength(50);
        builder.Property(e => e.Reason).IsRequired().HasMaxLength(500);
        builder.Property(e => e.ClassifiedBy).HasMaxLength(200);
        builder.Property(e => e.Confidence);  // nullable double → double precision; no type override needed

        builder.HasIndex(e => new { e.ReceiptItemId, e.ClassifiedAt })
            .IsDescending(false, true);

        builder.HasOne(e => e.ReceiptItem)
            .WithMany(e => e.Classifications)
            .HasForeignKey(e => e.ReceiptItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
