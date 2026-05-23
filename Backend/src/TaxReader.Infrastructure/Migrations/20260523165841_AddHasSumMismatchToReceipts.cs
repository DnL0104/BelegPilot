using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxReader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHasSumMismatchToReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_sum_mismatch",
                table: "receipts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_sum_mismatch",
                table: "receipts");
        }
    }
}
