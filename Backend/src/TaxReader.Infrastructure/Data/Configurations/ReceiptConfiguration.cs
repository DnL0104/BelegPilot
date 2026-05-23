using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Data.Configurations;

public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable("receipts");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.Vendor).IsRequired().HasMaxLength(500);
        builder.Property(e => e.SubTotal).HasColumnType("decimal(18,2)");
        builder.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
        builder.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(10).HasDefaultValue("EUR");
        builder.Property(e => e.RawExtractedText).IsRequired();
        builder.Property(e => e.HasSumMismatch)
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(e => e.ReceiptFileId).IsUnique();
        builder.HasIndex(e => e.PurchaseDate);

        builder.HasOne(e => e.ReceiptFile)
            .WithOne(e => e.Receipt)
            .HasForeignKey<Receipt>(e => e.ReceiptFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
