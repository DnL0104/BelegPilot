using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxReader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateClassificationRuleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_classification_rules_is_active_priority",
                table: "classification_rules");

            // Preserve existing keyword data: rename pattern → description_pattern instead of drop+add
            migrationBuilder.RenameColumn(
                name: "pattern",
                table: "classification_rules",
                newName: "description_pattern");

            migrationBuilder.AddColumn<string>(
                name: "source_file_pattern",
                table: "classification_rules",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "classification_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vendor_pattern",
                table: "classification_rules",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000001"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Tinte", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000002"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Papier", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000003"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Druckerpatrone", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000004"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Kugelschreiber", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000005"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Ordner", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000006"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Hefter", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000007"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Stift", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000008"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Klebeband", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000009"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Radiergummi", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000010"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Lineal", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000001"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Buch", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000002"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Fachbuch", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000003"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Lehrbuch", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000004"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Unterrichtsmaterial", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000005"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Arbeitsblatt", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000006"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Lernhilfe", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000001"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Eduki", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000002"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Arbeitsblätter", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000003"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Laminierfolie", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000004"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Whiteboard", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000005"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Kreide", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000006"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Tafel", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000007"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Bastelmaterial", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000008"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Poster", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a4000000-0000-0000-0000-000000000001"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Software", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a4000000-0000-0000-0000-000000000002"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Lizenz", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a4000000-0000-0000-0000-000000000003"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "App", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a4000000-0000-0000-0000-000000000004"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "USB", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000001"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Drucker", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000002"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Monitor", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000003"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Tastatur", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000004"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Maus", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000005"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Headset", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000006"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Laminator", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000007"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Schreibtisch", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000008"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Stuhl", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000009"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Mauspad", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a7000000-0000-0000-0000-000000000001"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Fortbildung", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a7000000-0000-0000-0000-000000000002"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Seminar", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a7000000-0000-0000-0000-000000000003"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Kurs", null, null, null });

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a7000000-0000-0000-0000-000000000004"),
                columns: new[] { "description_pattern", "source_file_pattern", "user_id", "vendor_pattern" },
                values: new object[] { "Workshop", null, null, null });

            migrationBuilder.CreateIndex(
                name: "ix_classification_rules_user_id_is_active_priority",
                table: "classification_rules",
                columns: new[] { "user_id", "is_active", "priority" });

            migrationBuilder.AddForeignKey(
                name: "fk_classification_rules_users_user_id",
                table: "classification_rules",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_classification_rules_users_user_id",
                table: "classification_rules");

            migrationBuilder.DropIndex(
                name: "ix_classification_rules_user_id_is_active_priority",
                table: "classification_rules");

            // Reverse the rename: description_pattern → pattern
            migrationBuilder.RenameColumn(
                name: "description_pattern",
                table: "classification_rules",
                newName: "pattern");

            migrationBuilder.DropColumn(
                name: "source_file_pattern",
                table: "classification_rules");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "classification_rules");

            migrationBuilder.DropColumn(
                name: "vendor_pattern",
                table: "classification_rules");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000001"),
                column: "pattern",
                value: "Tinte");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000002"),
                column: "pattern",
                value: "Papier");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000003"),
                column: "pattern",
                value: "Druckerpatrone");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000004"),
                column: "pattern",
                value: "Kugelschreiber");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000005"),
                column: "pattern",
                value: "Ordner");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000006"),
                column: "pattern",
                value: "Hefter");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000007"),
                column: "pattern",
                value: "Stift");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000008"),
                column: "pattern",
                value: "Klebeband");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000009"),
                column: "pattern",
                value: "Radiergummi");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a1000000-0000-0000-0000-000000000010"),
                column: "pattern",
                value: "Lineal");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000001"),
                column: "pattern",
                value: "Buch");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000002"),
                column: "pattern",
                value: "Fachbuch");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000003"),
                column: "pattern",
                value: "Lehrbuch");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000004"),
                column: "pattern",
                value: "Unterrichtsmaterial");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000005"),
                column: "pattern",
                value: "Arbeitsblatt");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a2000000-0000-0000-0000-000000000006"),
                column: "pattern",
                value: "Lernhilfe");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000001"),
                column: "pattern",
                value: "Eduki");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000002"),
                column: "pattern",
                value: "Arbeitsblätter");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000003"),
                column: "pattern",
                value: "Laminierfolie");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000004"),
                column: "pattern",
                value: "Whiteboard");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000005"),
                column: "pattern",
                value: "Kreide");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000006"),
                column: "pattern",
                value: "Tafel");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000007"),
                column: "pattern",
                value: "Bastelmaterial");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a3000000-0000-0000-0000-000000000008"),
                column: "pattern",
                value: "Poster");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a4000000-0000-0000-0000-000000000001"),
                column: "pattern",
                value: "Software");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a4000000-0000-0000-0000-000000000002"),
                column: "pattern",
                value: "Lizenz");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a4000000-0000-0000-0000-000000000003"),
                column: "pattern",
                value: "App");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a4000000-0000-0000-0000-000000000004"),
                column: "pattern",
                value: "USB");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000001"),
                column: "pattern",
                value: "Drucker");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000002"),
                column: "pattern",
                value: "Monitor");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000003"),
                column: "pattern",
                value: "Tastatur");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000004"),
                column: "pattern",
                value: "Maus");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000005"),
                column: "pattern",
                value: "Headset");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000006"),
                column: "pattern",
                value: "Laminator");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000007"),
                column: "pattern",
                value: "Schreibtisch");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000008"),
                column: "pattern",
                value: "Stuhl");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a5000000-0000-0000-0000-000000000009"),
                column: "pattern",
                value: "Mauspad");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a7000000-0000-0000-0000-000000000001"),
                column: "pattern",
                value: "Fortbildung");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a7000000-0000-0000-0000-000000000002"),
                column: "pattern",
                value: "Seminar");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a7000000-0000-0000-0000-000000000003"),
                column: "pattern",
                value: "Kurs");

            migrationBuilder.UpdateData(
                table: "classification_rules",
                keyColumn: "id",
                keyValue: new Guid("a7000000-0000-0000-0000-000000000004"),
                column: "pattern",
                value: "Workshop");

            migrationBuilder.CreateIndex(
                name: "ix_classification_rules_is_active_priority",
                table: "classification_rules",
                columns: new[] { "is_active", "priority" });
        }
    }
}
