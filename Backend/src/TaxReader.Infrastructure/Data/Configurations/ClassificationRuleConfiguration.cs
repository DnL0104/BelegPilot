using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;

namespace TaxReader.Infrastructure.Data.Configurations;

public class ClassificationRuleConfiguration : IEntityTypeConfiguration<ClassificationRule>
{
    public void Configure(EntityTypeBuilder<ClassificationRule> builder)
    {
        builder.ToTable("classification_rules");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.DescriptionPattern).HasMaxLength(500);
        builder.Property(e => e.VendorPattern).HasMaxLength(500);
        builder.Property(e => e.SourceFilePattern).HasMaxLength(500);
        builder.Property(e => e.UserId); // nullable Guid? — no max length, no IsRequired
        builder.Property(e => e.Category).HasConversion<string>().IsRequired().HasMaxLength(100);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.IsActive, e.Priority });

        // Seed initial classification rules
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Tinte",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000002"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Papier",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000003"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Druckerpatrone",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000004"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Kugelschreiber",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000005"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Ordner",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000006"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Hefter",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000007"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Stift",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000008"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Klebeband",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000009"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Radiergummi",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000010"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Lineal",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a2000000-0000-0000-0000-000000000001"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Buch",
                Category = Category.WerbungskostenFachliteratur,
                Priority = 20,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a2000000-0000-0000-0000-000000000002"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Fachbuch",
                Category = Category.WerbungskostenFachliteratur,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a2000000-0000-0000-0000-000000000003"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Lehrbuch",
                Category = Category.WerbungskostenFachliteratur,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a2000000-0000-0000-0000-000000000004"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Unterrichtsmaterial",
                Category = Category.WerbungskostenFachliteratur,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a2000000-0000-0000-0000-000000000005"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Arbeitsblatt",
                Category = Category.WerbungskostenFachliteratur,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a2000000-0000-0000-0000-000000000006"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Lernhilfe",
                Category = Category.WerbungskostenFachliteratur,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            // TeachingMaterials
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000001"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Eduki",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 5,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000002"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Arbeitsblätter",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000003"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Laminierfolie",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000004"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Whiteboard",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000005"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Kreide",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000006"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Tafel",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000007"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Bastelmaterial",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000008"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Poster",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 15,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            // DigitalToolsAndSoftware
            new ClassificationRule
            {
                Id = Guid.Parse("a4000000-0000-0000-0000-000000000001"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Software",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a4000000-0000-0000-0000-000000000002"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Lizenz",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a4000000-0000-0000-0000-000000000003"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "App",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 20,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a4000000-0000-0000-0000-000000000004"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "USB",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 15,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            // OfficeEquipment
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000001"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Drucker",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000002"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Monitor",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000003"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Tastatur",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000004"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Maus",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 15,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000005"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Headset",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000006"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Laminator",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000007"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Schreibtisch",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000008"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Stuhl",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 15,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000009"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Mauspad",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            // ProfessionalDevelopment
            new ClassificationRule
            {
                Id = Guid.Parse("a7000000-0000-0000-0000-000000000001"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Fortbildung",
                Category = Category.WerbungskostenFortbildung,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a7000000-0000-0000-0000-000000000002"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Seminar",
                Category = Category.WerbungskostenFortbildung,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a7000000-0000-0000-0000-000000000003"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Kurs",
                Category = Category.WerbungskostenFortbildung,
                Priority = 15,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a7000000-0000-0000-0000-000000000004"),
                UserId = (Guid?)null,
                VendorPattern = null,
                SourceFilePattern = null,
                DescriptionPattern = "Workshop",
                Category = Category.WerbungskostenFortbildung,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            }
        );
    }
}
