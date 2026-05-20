using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Infrastructure.Data.Configurations;

public class ReceiptFileConfiguration : IEntityTypeConfiguration<ReceiptFile>
{
    public void Configure(EntityTypeBuilder<ReceiptFile> builder)
    {
        builder.ToTable("receipt_files");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(500);
        builder.Property(e => e.ContentHash).IsRequired().HasMaxLength(128);
        builder.Property(e => e.SourceHint).HasMaxLength(100);
        builder.Property(e => e.UploadedBy).HasMaxLength(200);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(e => new { e.UserId, e.ContentHash }).IsUnique();
        builder.HasIndex(e => e.UploadedAt);
    }
}
