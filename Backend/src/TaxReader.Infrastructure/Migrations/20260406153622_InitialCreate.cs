using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TaxReader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "classification_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    pattern = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_classification_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "receipt_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    original_file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    storage_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    content_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    source_hint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    year_hint = table.Column<int>(type: "integer", nullable: true),
                    uploaded_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipt_files", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processing_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    receipt_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    step_details = table.Column<string>(type: "text", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processing_runs", x => x.id);
                    table.ForeignKey(
                        name: "fk_processing_runs_receipt_files_receipt_file_id",
                        column: x => x.receipt_file_id,
                        principalTable: "receipt_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    receipt_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vendor = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    purchase_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sub_total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "EUR"),
                    raw_extracted_text = table.Column<string>(type: "text", nullable: false),
                    parsed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipts", x => x.id);
                    table.ForeignKey(
                        name: "fk_receipts_receipt_files_receipt_file_id",
                        column: x => x.receipt_file_id,
                        principalTable: "receipt_files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "receipt_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipt_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_receipt_items_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalTable: "receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_classifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    receipt_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    classified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    classified_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_classifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_item_classifications_receipt_items_receipt_item_id",
                        column: x => x.receipt_item_id,
                        principalTable: "receipt_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "classification_rules",
                columns: new[] { "id", "category", "created_at", "is_active", "pattern", "priority", "updated_at" },
                values: new object[,]
                {
                    { new Guid("a1000000-0000-0000-0000-000000000001"), "ConsumablesAndOfficeSupplies", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Tinte", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1000000-0000-0000-0000-000000000002"), "ConsumablesAndOfficeSupplies", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Papier", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1000000-0000-0000-0000-000000000003"), "ConsumablesAndOfficeSupplies", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Druckerpatrone", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1000000-0000-0000-0000-000000000004"), "ConsumablesAndOfficeSupplies", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Kugelschreiber", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1000000-0000-0000-0000-000000000005"), "ConsumablesAndOfficeSupplies", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Ordner", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1000000-0000-0000-0000-000000000006"), "ConsumablesAndOfficeSupplies", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Hefter", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1000000-0000-0000-0000-000000000007"), "ConsumablesAndOfficeSupplies", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Stift", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1000000-0000-0000-0000-000000000008"), "ConsumablesAndOfficeSupplies", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Klebeband", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1000000-0000-0000-0000-000000000009"), "ConsumablesAndOfficeSupplies", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Radiergummi", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1000000-0000-0000-0000-000000000010"), "ConsumablesAndOfficeSupplies", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Lineal", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a2000000-0000-0000-0000-000000000001"), "SpecialistLiterature", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Buch", 20, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a2000000-0000-0000-0000-000000000002"), "SpecialistLiterature", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Fachbuch", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a2000000-0000-0000-0000-000000000003"), "SpecialistLiterature", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Lehrbuch", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a2000000-0000-0000-0000-000000000004"), "SpecialistLiterature", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Unterrichtsmaterial", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a2000000-0000-0000-0000-000000000005"), "SpecialistLiterature", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Arbeitsblatt", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a2000000-0000-0000-0000-000000000006"), "SpecialistLiterature", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "Lernhilfe", 10, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_classification_rules_is_active_priority",
                table: "classification_rules",
                columns: new[] { "is_active", "priority" });

            migrationBuilder.CreateIndex(
                name: "ix_item_classifications_receipt_item_id_classified_at",
                table: "item_classifications",
                columns: new[] { "receipt_item_id", "classified_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_processing_runs_receipt_file_id",
                table: "processing_runs",
                column: "receipt_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_receipt_files_content_hash",
                table: "receipt_files",
                column: "content_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_receipt_files_uploaded_at",
                table: "receipt_files",
                column: "uploaded_at");

            migrationBuilder.CreateIndex(
                name: "ix_receipt_items_receipt_id",
                table: "receipt_items",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "ix_receipts_purchase_date",
                table: "receipts",
                column: "purchase_date");

            migrationBuilder.CreateIndex(
                name: "ix_receipts_receipt_file_id",
                table: "receipts",
                column: "receipt_file_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "classification_rules");

            migrationBuilder.DropTable(
                name: "item_classifications");

            migrationBuilder.DropTable(
                name: "processing_runs");

            migrationBuilder.DropTable(
                name: "receipt_items");

            migrationBuilder.DropTable(
                name: "receipts");

            migrationBuilder.DropTable(
                name: "receipt_files");
        }
    }
}
