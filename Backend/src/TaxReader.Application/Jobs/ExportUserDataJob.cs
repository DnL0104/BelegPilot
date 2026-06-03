using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using TaxReader.Application.Interfaces;

namespace TaxReader.Application.Jobs;

/// <summary>
/// Hangfire fire-and-forget job that assembles a DSGVO Art. 20 export bundle for a user.
/// Writes JSON + CSV entries for receipts, items, classifications, token_transactions,
/// the user's own audit_log rows (D-15), and README.txt into a zip at
/// /tmp/taxreader-exports/{exportToken}.zip. Marks the ExportTokenStore Ready on completion.
///
/// D-11: Bundle excludes password hash and all internal-only fields.
/// T-06-44: No PasswordHash projected into any shape.
/// T-06-46: Audit query filters SubjectUserId == userId.
/// Pattern: GrantTokensJob (primary-ctor DI, [AutomaticRetry], LogContext.PushProperty, no ICurrentUser).
/// </summary>
public class ExportUserDataJob(
    IAppDbContext dbContext,
    ILogger<ExportUserDataJob> logger,
    IExportTokenStore tokenStore)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public async Task HandleAsync(Guid userId, string exportToken, CancellationToken cancellationToken)
    {
        using var _ = LogContext.PushProperty("JobId", $"Export_{userId}");

        logger.LogInformation("Starting data export for User {UserId}, token prefix {TokenPrefix}",
            userId, exportToken.Length >= 8 ? exportToken[..8] : exportToken);

        // 1. Query all user-scoped data (no PasswordHash, no internal noise)
        var receipts = await dbContext.ReceiptFiles
            .Where(f => f.UserId == userId)
            .Select(f => new
            {
                id = f.Id,
                original_file_name = f.OriginalFileName,
                uploaded_at = f.UploadedAt,
                status = f.Status.ToString(),
                file_size = f.FileSize,
                source_hint = f.SourceHint
            })
            .ToListAsync(cancellationToken);

        var parsedReceipts = await dbContext.Receipts
            .Where(r => r.ReceiptFile.UserId == userId)
            .Select(r => new
            {
                id = r.Id,
                receipt_file_id = r.ReceiptFileId,
                vendor = r.Vendor,
                purchase_date = r.PurchaseDate.ToString("yyyy-MM-dd"),
                total_amount = r.TotalAmount,
                currency = r.Currency,
                parsed_at = r.ParsedAt
            })
            .ToListAsync(cancellationToken);

        var items = await dbContext.ReceiptItems
            .Where(i => i.Receipt.ReceiptFile.UserId == userId)
            .Select(i => new
            {
                id = i.Id,
                receipt_id = i.ReceiptId,
                description = i.Description,
                quantity = i.Quantity,
                unit_price = i.UnitPrice,
                total_price = i.TotalPrice,
                line_number = i.LineNumber
            })
            .ToListAsync(cancellationToken);

        var classifications = await dbContext.ItemClassifications
            .Where(c => c.ReceiptItem.Receipt.ReceiptFile.UserId == userId)
            .Select(c => new
            {
                id = c.Id,
                receipt_item_id = c.ReceiptItemId,
                category = c.Category.ToString(),
                method = c.Method.ToString(),
                status = c.Status.ToString(),
                reason = c.Reason,
                classified_at = c.ClassifiedAt
            })
            .ToListAsync(cancellationToken);

        var tokenTransactions = await dbContext.TokenTransactions
            .Where(t => t.UserId == userId)
            .Select(t => new
            {
                id = t.Id,
                type = t.Type.ToString(),
                amount = t.Amount,
                balance_after = t.BalanceAfter,
                description = t.Description,
                created_at = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // T-06-46: filter audit_log strictly by SubjectUserId == userId (only own rows)
        var auditEntries = await dbContext.AuditLogEntries
            .Where(a => a.SubjectUserId == userId)
            .Select(a => new
            {
                id = a.Id,
                action = a.Action,
                actor_user_id = a.ActorUserId,
                subject_user_id = a.SubjectUserId,
                created_at = a.CreatedAt
                // metadata is excluded — may contain hashed PII; included separately below
            })
            .ToListAsync(cancellationToken);

        // 2. Create zip archive
        var exportsDir = Path.Combine(Path.GetTempPath(), "taxreader-exports");
        Directory.CreateDirectory(exportsDir);
        var zipPath = Path.Combine(exportsDir, exportToken + ".zip");

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
        {
            await WriteJsonEntryAsync(archive, "receipts.json", receipts, jsonOptions, cancellationToken);
            await WriteCsvEntryAsync(archive, "receipts.csv",
                "id,original_file_name,uploaded_at,status,file_size,source_hint",
                receipts.Select(r => $"{r.id},{EscapeCsv(r.original_file_name)},{r.uploaded_at:o},{r.status},{r.file_size},{EscapeCsv(r.source_hint)}"),
                cancellationToken);

            await WriteJsonEntryAsync(archive, "items.json", items, jsonOptions, cancellationToken);
            await WriteCsvEntryAsync(archive, "items.csv",
                "id,receipt_id,description,quantity,unit_price,total_price,line_number",
                items.Select(i => $"{i.id},{i.receipt_id},{EscapeCsv(i.description)},{i.quantity},{i.unit_price},{i.total_price},{i.line_number}"),
                cancellationToken);

            await WriteJsonEntryAsync(archive, "classifications.json", classifications, jsonOptions, cancellationToken);
            await WriteCsvEntryAsync(archive, "classifications.csv",
                "id,receipt_item_id,category,method,status,reason,classified_at",
                classifications.Select(c => $"{c.id},{c.receipt_item_id},{c.category},{c.method},{c.status},{EscapeCsv(c.reason)},{c.classified_at:o}"),
                cancellationToken);

            await WriteJsonEntryAsync(archive, "token_transactions.json", tokenTransactions, jsonOptions, cancellationToken);
            await WriteCsvEntryAsync(archive, "token_transactions.csv",
                "id,type,amount,balance_after,description,created_at",
                tokenTransactions.Select(t => $"{t.id},{t.type},{t.amount},{t.balance_after},{EscapeCsv(t.description)},{t.created_at:o}"),
                cancellationToken);

            await WriteJsonEntryAsync(archive, "audit_log.json", auditEntries, jsonOptions, cancellationToken);
            await WriteCsvEntryAsync(archive, "audit_log.csv",
                "id,action,actor_user_id,subject_user_id,created_at",
                auditEntries.Select(a => $"{a.id},{a.action},{a.actor_user_id},{a.subject_user_id},{a.created_at:o}"),
                cancellationToken);

            await WriteReadmeAsync(archive, cancellationToken);
        }

        // 3. Mark token Ready in the store (zip is fully written and closed)
        tokenStore.Register(exportToken, userId, DateTime.UtcNow.AddHours(24));

        logger.LogInformation("Data export completed for User {UserId}, token prefix {TokenPrefix}, size {SizeBytes} bytes",
            userId,
            exportToken.Length >= 8 ? exportToken[..8] : exportToken,
            new FileInfo(zipPath).Length);
    }

    private static async Task WriteJsonEntryAsync<T>(
        ZipArchive archive, string entryName, T data,
        JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, data, options, cancellationToken);
    }

    private static async Task WriteCsvEntryAsync(
        ZipArchive archive, string entryName,
        string header, IEnumerable<string> rows,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteLineAsync(header);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(row);
        }
    }

    private static async Task WriteReadmeAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry("README.txt", CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteAsync($"""
            TaxReader — Datenschutz-Export gemäß DSGVO Art. 20
            Erstellt: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC

            Inhalt:
            - receipts.json / receipts.csv: Ihre hochgeladenen Belege (Metadaten)
            - items.json / items.csv: Einzelpositionen aus den Belegen
            - classifications.json / classifications.csv: Klassifizierungen je Position
            - token_transactions.json / token_transactions.csv: Token-Transaktionen
            - audit_log.json / audit_log.csv: Protokoll sensitiver Vorgänge (Art. 15)

            Hinweise:
            - Sensible Felder (z. B. Passwort-Hash) sind nicht enthalten.
            - Der Export enthält ausschließlich Ihre eigenen Daten.
            - Gültig für 24 Stunden ab Erstellung.
            """);
    }

    private static string EscapeCsv(string? value)
    {
        if (value is null) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
