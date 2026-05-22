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

        builder.Property(e => e.Pattern).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Category).HasConversion<string>().IsRequired().HasMaxLength(100);

        builder.HasIndex(e => new { e.IsActive, e.Priority });

        // Seed initial classification rules
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        builder.HasData(
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                Pattern = "Tinte",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000002"),
                Pattern = "Papier",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000003"),
                Pattern = "Druckerpatrone",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000004"),
                Pattern = "Kugelschreiber",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000005"),
                Pattern = "Ordner",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000006"),
                Pattern = "Hefter",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000007"),
                Pattern = "Stift",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000008"),
                Pattern = "Klebeband",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000009"),
                Pattern = "Radiergummi",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000010"),
                Pattern = "Lineal",
                Category = Category.WerbungskostenBueromaterial,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a2000000-0000-0000-0000-000000000001"),
                Pattern = "Buch",
                Category = Category.WerbungskostenFachliteratur,
                Priority = 20,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a2000000-0000-0000-0000-000000000002"),
                Pattern = "Fachbuch",
                Category = Category.WerbungskostenFachliteratur,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a2000000-0000-0000-0000-000000000003"),
                Pattern = "Lehrbuch",
                Category = Category.WerbungskostenFachliteratur,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a2000000-0000-0000-0000-000000000004"),
                Pattern = "Unterrichtsmaterial",
                Category = Category.WerbungskostenFachliteratur,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a2000000-0000-0000-0000-000000000005"),
                Pattern = "Arbeitsblatt",
                Category = Category.WerbungskostenFachliteratur,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a2000000-0000-0000-0000-000000000006"),
                Pattern = "Lernhilfe",
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
                Pattern = "Eduki",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 5,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000002"),
                Pattern = "Arbeitsblätter",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000003"),
                Pattern = "Laminierfolie",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000004"),
                Pattern = "Whiteboard",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000005"),
                Pattern = "Kreide",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000006"),
                Pattern = "Tafel",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000007"),
                Pattern = "Bastelmaterial",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a3000000-0000-0000-0000-000000000008"),
                Pattern = "Poster",
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
                Pattern = "Software",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a4000000-0000-0000-0000-000000000002"),
                Pattern = "Lizenz",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a4000000-0000-0000-0000-000000000003"),
                Pattern = "App",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 20,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a4000000-0000-0000-0000-000000000004"),
                Pattern = "USB",
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
                Pattern = "Drucker",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000002"),
                Pattern = "Monitor",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000003"),
                Pattern = "Tastatur",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000004"),
                Pattern = "Maus",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 15,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000005"),
                Pattern = "Headset",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000006"),
                Pattern = "Laminator",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000007"),
                Pattern = "Schreibtisch",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000008"),
                Pattern = "Stuhl",
                Category = Category.WerbungskostenArbeitsmittel,
                Priority = 15,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a5000000-0000-0000-0000-000000000009"),
                Pattern = "Mauspad",
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
                Pattern = "Fortbildung",
                Category = Category.WerbungskostenFortbildung,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a7000000-0000-0000-0000-000000000002"),
                Pattern = "Seminar",
                Category = Category.WerbungskostenFortbildung,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a7000000-0000-0000-0000-000000000003"),
                Pattern = "Kurs",
                Category = Category.WerbungskostenFortbildung,
                Priority = 15,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new ClassificationRule
            {
                Id = Guid.Parse("a7000000-0000-0000-0000-000000000004"),
                Pattern = "Workshop",
                Category = Category.WerbungskostenFortbildung,
                Priority = 10,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            }
        );
    }
}
