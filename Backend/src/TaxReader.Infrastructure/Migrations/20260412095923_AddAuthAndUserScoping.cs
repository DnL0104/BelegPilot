using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxReader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthAndUserScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_token_balances_user_key",
                table: "user_token_balances");

            migrationBuilder.DropIndex(
                name: "ix_token_transactions_user_key_created_at",
                table: "token_transactions");

            migrationBuilder.DropIndex(
                name: "ix_receipt_files_content_hash",
                table: "receipt_files");

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "user_token_balances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "token_transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                table: "receipt_files",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    refresh_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    refresh_token_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_token_balances_user_id",
                table: "user_token_balances",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_token_transactions_user_id_created_at",
                table: "token_transactions",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_receipt_files_user_id_content_hash",
                table: "receipt_files",
                columns: new[] { "user_id", "content_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_receipt_files_users_user_id",
                table: "receipt_files",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_token_balances_users_user_id",
                table: "user_token_balances",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_receipt_files_users_user_id",
                table: "receipt_files");

            migrationBuilder.DropForeignKey(
                name: "fk_user_token_balances_users_user_id",
                table: "user_token_balances");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropIndex(
                name: "ix_user_token_balances_user_id",
                table: "user_token_balances");

            migrationBuilder.DropIndex(
                name: "ix_token_transactions_user_id_created_at",
                table: "token_transactions");

            migrationBuilder.DropIndex(
                name: "ix_receipt_files_user_id_content_hash",
                table: "receipt_files");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "user_token_balances");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "token_transactions");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "receipt_files");

            migrationBuilder.CreateIndex(
                name: "ix_user_token_balances_user_key",
                table: "user_token_balances",
                column: "user_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_token_transactions_user_key_created_at",
                table: "token_transactions",
                columns: new[] { "user_key", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_receipt_files_content_hash",
                table: "receipt_files",
                column: "content_hash",
                unique: true);
        }
    }
}
