using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxReader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AnonymizePaymentOnUserDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payments_users_user_id",
                table: "payments");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "fk_payments_users_user_id",
                table: "payments",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_payments_users_user_id",
                table: "payments");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_payments_users_user_id",
                table: "payments",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
