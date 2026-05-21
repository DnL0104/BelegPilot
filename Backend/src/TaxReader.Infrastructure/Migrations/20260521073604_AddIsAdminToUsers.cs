using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxReader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsAdminToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-07: introduces IsAdmin column with NOT NULL DEFAULT false. Safe to apply against existing rows
            // (defaultValue: false → all existing users start as non-admin). Admin promotion happens via
            // SeedAdminUsersHostedService at startup (D-08) reading Hangfire__SeedAdminEmails.
            migrationBuilder.AddColumn<bool>(
                name: "is_admin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_admin",
                table: "users");
        }
    }
}
