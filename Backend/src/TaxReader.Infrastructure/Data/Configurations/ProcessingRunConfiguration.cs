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

        // D-21: short stable code (e.g. NoTextExtracted, Cancelled). Pairs with German ErrorMessage.
        builder.Property(e => e.ErrorCode).HasMaxLength(50);

        // D-14: opaque Hangfire job ID; numeric strings today but the contract is opaque.
        builder.Property(e => e.HangfireJobId).HasMaxLength(100);

        builder.HasOne(e => e.ReceiptFile)
            .WithMany(e => e.ProcessingRuns)
            .HasForeignKey(e => e.ReceiptFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
