using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxReader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "token_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    balance_after = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    related_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_token_transactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_token_balances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    balance = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_token_balances", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "classification_rules",
                columns: new[] { "id", "category", "created_at", "is_active", "pattern", "priority", "updated_at" },
                values: new object[,]
                {
                    { new Guid("a3000000-0000-0000-0000-000000000001"), "TeachingMaterials", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Eduki", 5, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a3000000-0000-0000-0000-000000000002"), "TeachingMaterials", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Arbeitsblätter", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a3000000-0000-0000-0000-000000000003"), "TeachingMaterials", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Laminierfolie", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a3000000-0000-0000-0000-000000000004"), "TeachingMaterials", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Whiteboard", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a3000000-0000-0000-0000-000000000005"), "TeachingMaterials", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Kreide", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a3000000-0000-0000-0000-000000000006"), "TeachingMaterials", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Tafel", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a3000000-0000-0000-0000-000000000007"), "TeachingMaterials", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Bastelmaterial", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a3000000-0000-0000-0000-000000000008"), "TeachingMaterials", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Poster", 15, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a4000000-0000-0000-0000-000000000001"), "DigitalToolsAndSoftware", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Software", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a4000000-0000-0000-0000-000000000002"), "DigitalToolsAndSoftware", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Lizenz", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a4000000-0000-0000-0000-000000000003"), "DigitalToolsAndSoftware", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "App", 20, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a4000000-0000-0000-0000-000000000004"), "DigitalToolsAndSoftware", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "USB", 15, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a5000000-0000-0000-0000-000000000001"), "OfficeEquipment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Drucker", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a5000000-0000-0000-0000-000000000002"), "OfficeEquipment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Monitor", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a5000000-0000-0000-0000-000000000003"), "OfficeEquipment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Tastatur", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a5000000-0000-0000-0000-000000000004"), "OfficeEquipment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Maus", 15, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a5000000-0000-0000-0000-000000000005"), "OfficeEquipment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Headset", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a5000000-0000-0000-0000-000000000006"), "OfficeEquipment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Laminator", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a5000000-0000-0000-0000-000000000007"), "OfficeEquipment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Schreibtisch", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a5000000-0000-0000-0000-000000000008"), "OfficeEquipment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Stuhl", 15, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a5000000-0000-0000-0000-000000000009"), "OfficeEquipment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Mauspad", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a7000000-0000-0000-0000-000000000001"), "ProfessionalDevelopment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Fortbildung", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a7000000-0000-0000-0000-000000000002"), "ProfessionalDevelopment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Seminar", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a7000000-0000-0000-0000-000000000003"), "ProfessionalDevelopment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Kurs", 15, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a7000000-0000-0000-0000-000000000004"), "ProfessionalDevelopment", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Workshop", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_token_transactions_user_key_created_at",
                table: "token_transactions",
                columns: new[] { "user_key", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_user_token_balances_user_key",
                table: "user_token_balances",
                column: "user_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "token_transactions");

            migrationBuilder.DropTable(
                name: "user_token_balances");

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a4000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a4000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a4000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a4000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a7000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a7000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a7000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a7000000-0000-0000-0000-000000000004"));
        }
    }
}
