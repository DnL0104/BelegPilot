using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Data.Configurations;

public class ReceiptItemConfiguration : IEntityTypeConfiguration<ReceiptItem>
{
    public void Configure(EntityTypeBuilder<ReceiptItem> builder)
    {
        builder.ToTable("receipt_items");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.Description).IsRequired().HasMaxLength(1000);
        builder.Property(e => e.Quantity).HasDefaultValue(1);
        builder.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
        builder.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");

        builder.HasIndex(e => e.ReceiptId);

        builder.HasOne(e => e.Receipt)
            .WithMany(e => e.Items)
            .HasForeignKey(e => e.ReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(e => e.LatestClassification);
    }
}
