using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxReader.Domain.Entities;

namespace TaxReader.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        // Base64 of 32-byte HMAC-SHA256 is exactly 44 chars.
        builder.Property(e => e.TokenHash).IsRequired().HasMaxLength(44);
        builder.Property(e => e.UserAgent).HasMaxLength(500);

        // O(1) lookup on the hash column; unique because two distinct plaintexts
        // hashing to the same Base64 value would require a SHA256 collision.
        builder.HasIndex(e => e.TokenHash).IsUnique();

        // Composite index supports "revoke all non-revoked for user" queries and
        // active-session listing (D-02). Postgres-side: index size stays small
        // because most rows have RevokedAt set after the 30-day TTL window.
        builder.HasIndex(e => new { e.UserId, e.RevokedAt });

        // Self-FK to express the rotation chain; NoAction to avoid cascading
        // delete loops (parent row pointing at the just-rotated child).
        // User → RefreshTokens cascade is configured in UserConfiguration.
        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(e => e.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
