using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxReader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    metadata = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_created_at",
                table: "audit_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_subject_user_id",
                table: "audit_log",
                column: "subject_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");
        }
    }
}
