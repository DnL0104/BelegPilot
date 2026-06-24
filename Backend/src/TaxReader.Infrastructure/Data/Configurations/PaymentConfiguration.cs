using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.StripeEventId).IsRequired().HasMaxLength(255);
        builder.HasIndex(e => e.StripeEventId).IsUnique();
        builder.Property(e => e.StripeSessionId).IsRequired().HasMaxLength(255);
        builder.Property(e => e.StripePaymentIntentId).HasMaxLength(255);
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        builder.Property(e => e.Status).HasConversion<int>();
        // D-04: retain anonymized Payment rows on account deletion (§257 HGB / §147 AO Aufbewahrungspflicht).
        // SET NULL fires automatically when the User row is deleted; no explicit Payment manipulation needed.
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
    }
}
