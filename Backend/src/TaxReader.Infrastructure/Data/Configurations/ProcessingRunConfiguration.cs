using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Data.Configurations;

public class ProcessingRunConfiguration : IEntityTypeConfiguration<ProcessingRun>
{
    public void Configure(EntityTypeBuilder<ProcessingRun> builder)
    {
        builder.ToTable("processing_runs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.Status).HasConversion<string>().IsRequired().HasMaxLength(50);
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);
        builder.Property(e => e.StepDetails).HasDefaultValue("[]");

        builder.HasOne(e => e.ReceiptFile)
            .WithMany(e => e.ProcessingRuns)
            .HasForeignKey(e => e.ReceiptFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
