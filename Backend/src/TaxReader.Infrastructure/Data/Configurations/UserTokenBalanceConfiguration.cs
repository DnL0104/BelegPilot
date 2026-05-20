using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Data.Configurations;

public class UserTokenBalanceConfiguration : IEntityTypeConfiguration<UserTokenBalance>
{
    public void Configure(EntityTypeBuilder<UserTokenBalance> builder)
    {
        builder.ToTable("user_token_balances");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.UserKey).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Balance).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        builder.HasIndex(e => e.UserId).IsUnique();
    }
}
