using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxReader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConfidenceAndFailed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ClassificationStatus.Failed is string-stored ("Failed") via HasConversion<string>() —
            // no structural SQL is needed for the new enum value; only the confidence column is added.
            migrationBuilder.AddColumn<double>(
                name: "confidence",
                table: "item_classifications",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "confidence",
                table: "item_classifications");
        }
    }
}
