using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxReader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenTransactionUserFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove any orphaned rows before adding the FK constraint,
            // so the migration succeeds even on databases with stale dev data.
            migrationBuilder.Sql("""
                DELETE FROM token_transactions
                WHERE user_id NOT IN (SELECT id FROM users);
                """);

            migrationBuilder.AddForeignKey(
                name: "fk_token_transactions_users_user_id",
                table: "token_transactions",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_token_transactions_users_user_id",
                table: "token_transactions");
        }
    }
}
