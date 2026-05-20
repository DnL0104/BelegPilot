using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Data.Configurations;

public class TokenTransactionConfiguration : IEntityTypeConfiguration<TokenTransaction>
{
    public void Configure(EntityTypeBuilder<TokenTransaction> builder)
    {
        builder.ToTable("token_transactions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.UserKey).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Type).HasConversion<string>().IsRequired().HasMaxLength(50);
        builder.Property(e => e.Amount).IsRequired();
        builder.Property(e => e.BalanceAfter).IsRequired();
        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => new { e.UserId, e.CreatedAt })
            .IsDescending(false, true);

        builder.HasOne(e => e.User)
            .WithMany(u => u.TokenTransactions)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
