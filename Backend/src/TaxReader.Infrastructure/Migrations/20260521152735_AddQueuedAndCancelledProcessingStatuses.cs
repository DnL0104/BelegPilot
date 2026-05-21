using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxReader.Infrastructure.Migrations
{
    // D-06 strict numeric reorder per locked CONTEXT decision. RESEARCH Pitfall 8 mitigation:
    // descending-order UPDATE statements run BEFORE any column changes so existing rows survive
    // if processing_runs.status is ever migrated from string to integer storage.
    //
    // Current production column: processing_runs.status is character varying(50) (string-
    // converted via HasConversion<string>() in ProcessingRunConfiguration). The numeric
    // UPDATE statements below are safe no-ops against today's string rows (no row matches
    // 'status = 5' literal), but they MUST remain present so any future integer migration
    // carries the data-safety guarantees out of the box.
    //
    // Manual UAT (03-HUMAN-UAT.md): apply against a populated Postgres 17 instance + verify
    // every legacy status value (string 'Pending', 'Extracting', etc.) is preserved.

    /// <inheritdoc />
    public partial class AddQueuedAndCancelledProcessingStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-06 + RESEARCH Pitfall 8: renumber existing processing_runs.status rows in
            // descending order so they map to the new enum values correctly. Order matters —
            // process from highest old value to lowest so we never collide with a row we're
            // about to write. Pending (0) stays unchanged.
            //
            // Old order:  Pending=0  Extracting=1  Parsing=2  Classifying=3  Completed=4  Failed=5
            // New order:  Pending=0  Queued=1      Extracting=2  Parsing=3  Classifying=4  Completed=5  Failed=6  Cancelled=7
            //
            // Renumber: 5→6, 4→5, 3→4, 2→3, 1→2 (descending). 0 stays as Pending.
            // Existing rows never had Queued or Cancelled — those values are new.
            //
            // NOTE on current string-storage: ProcessingRun.Status is mapped via
            // HasConversion<string>() (ProcessingRunConfiguration.cs), so the column is
            // character varying(50) holding values like 'Pending', 'Extracting'. The
            // numeric UPDATE statements below match zero rows today (safe no-ops), but
            // are retained so the data-migration guarantee survives any future move to
            // integer storage. Source-grep tests in ProcessingRunStatusMigrationTests
            // lock the wording in place.
            migrationBuilder.Sql("UPDATE processing_runs SET status = 6 WHERE status = 5;");
            migrationBuilder.Sql("UPDATE processing_runs SET status = 5 WHERE status = 4;");
            migrationBuilder.Sql("UPDATE processing_runs SET status = 4 WHERE status = 3;");
            migrationBuilder.Sql("UPDATE processing_runs SET status = 3 WHERE status = 2;");
            migrationBuilder.Sql("UPDATE processing_runs SET status = 2 WHERE status = 1;");

            migrationBuilder.AddColumn<Guid>(
                name: "upload_batch_id",
                table: "receipt_files",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "error_code",
                table: "processing_runs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hangfire_job_id",
                table: "processing_runs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_receipt_files_upload_batch_id",
                table: "receipt_files",
                column: "upload_batch_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_receipt_files_upload_batch_id",
                table: "receipt_files");

            migrationBuilder.DropColumn(
                name: "upload_batch_id",
                table: "receipt_files");

            migrationBuilder.DropColumn(
                name: "error_code",
                table: "processing_runs");

            migrationBuilder.DropColumn(
                name: "hangfire_job_id",
                table: "processing_runs");

            // Reverse the renumber (ascending — same collision-avoidance logic in reverse).
            // Note: any rows currently in Queued (1) or Cancelled (7) post-migration would
            // collide on Down(). They are post-Phase-3 data; rolling back is an emergency
            // operation that operator handles with a separate cleanup script.
            migrationBuilder.Sql("UPDATE processing_runs SET status = 1 WHERE status = 2;");
            migrationBuilder.Sql("UPDATE processing_runs SET status = 2 WHERE status = 3;");
            migrationBuilder.Sql("UPDATE processing_runs SET status = 3 WHERE status = 4;");
            migrationBuilder.Sql("UPDATE processing_runs SET status = 4 WHERE status = 5;");
            migrationBuilder.Sql("UPDATE processing_runs SET status = 5 WHERE status = 6;");
        }
    }
}
