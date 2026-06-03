using TaxReader.Application.Interfaces;
using TaxReader.Application.Jobs;
using TaxReader.Domain.Enums;

namespace TaxReader.Api.Endpoints;

/// <summary>
/// DSGVO Art. 20 self-serve data export endpoints.
/// All three endpoints are under /api/v1 (RequireAuthorization) — no AllowAnonymous.
/// LEG-07 / D-09 / D-10 / T-06-40 / T-06-41 / T-06-42 / T-06-45.
/// </summary>
public static class ExportEndpoints
{
    public static RouteGroupBuilder MapExportEndpoints(this RouteGroupBuilder group)
    {
        var export = group.MapGroup("/export").WithTags("Export");

        // POST /export/request — trigger a new export bundle generation
        export.MapPost("/request", async (
            ICurrentUser currentUser,
            IBackgroundJobClient jobClient,
            IExportTokenStore tokenStore,
            IAuditLogger auditLogger,
            CancellationToken cancellationToken) =>
        {
            // T-06-41: token = Guid.ToString("N") — 128-bit CSPRNG, opaque, non-sequential
            var token = Guid.NewGuid().ToString("N");

            // Mark as Generating in the store before enqueuing, so status polling works immediately
            tokenStore.MarkGenerating(token, currentUser.UserId, DateTime.UtcNow.AddHours(24));

            await jobClient.EnqueueAsync<ExportUserDataJob>(
                j => j.HandleAsync(currentUser.UserId, token, CancellationToken.None),
                cancellationToken);

            // T-06-41: only log the first 8 chars — do NOT log the full token
            await auditLogger.RecordAsync(
                AuditAction.DataExportRequested,
                actorUserId: currentUser.UserId,
                subjectUserId: currentUser.UserId,
                metadata: new Dictionary<string, object?> { ["token_prefix"] = token[..8] },
                cancellationToken);

            return Results.Ok(new { exportToken = token });
        })
        .WithName("RequestDataExport")
        .WithSummary("Request a DSGVO Art. 20 data export bundle");

        // GET /export/status?token= — poll bundle generation status
        export.MapGet("/status", (
            string token,
            ICurrentUser currentUser,
            IExportTokenStore tokenStore) =>
        {
            if (!tokenStore.TryGet(token, out var record) || record!.UserId != currentUser.UserId)
            {
                // T-06-41: do NOT leak existence of another user's token — return Expired for all unknown/foreign tokens
                return Results.Ok(new { status = "Expired" });
            }

            return Results.Ok(new { status = record.Status.ToString() });
        })
        .WithName("GetExportStatus")
        .WithSummary("Poll the status of a data export bundle");

        // GET /export/download?token= — download the export bundle (one-time)
        export.MapGet("/download", async (
            string token,
            ICurrentUser currentUser,
            IExportTokenStore tokenStore,
            IAuditLogger auditLogger,
            CancellationToken cancellationToken) =>
        {
            if (!tokenStore.TryGet(token, out var record))
            {
                return Results.NotFound(new { error = "Export not found or expired." });
            }

            // T-06-40: IDOR check — token must belong to the requesting user
            if (record!.UserId != currentUser.UserId)
            {
                return Results.Forbid();
            }

            // T-06-43: check expiry
            if (record.Status == ExportStatus.Expired)
            {
                return Results.Problem(
                    title: "Export Expired",
                    detail: "Der Export-Link ist abgelaufen. Bitte fordern Sie einen neuen Export an.",
                    statusCode: StatusCodes.Status410Gone);
            }

            if (record.Status == ExportStatus.Generating)
            {
                return Results.Problem(
                    title: "Export Not Ready",
                    detail: "Der Export wird noch erstellt. Bitte versuchen Sie es in wenigen Sekunden erneut.",
                    statusCode: StatusCodes.Status202Accepted);
            }

            // T-06-45: path is constructed from hex-only Guid token — no user-supplied path segments
            var zipPath = Path.Combine(Path.GetTempPath(), "taxreader-exports", token + ".zip");
            if (!File.Exists(zipPath))
            {
                // Token was ready but file is gone (container restart) — treat as expired
                tokenStore.Invalidate(token);
                return Results.Problem(
                    title: "Export Expired",
                    detail: "Der Export ist nicht mehr verfügbar. Bitte fordern Sie einen neuen Export an.",
                    statusCode: StatusCodes.Status410Gone);
            }

            // Stream the file, then invalidate the token (one-time — T-06-42)
            var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);

            // T-06-42: one-time invalidation after we've opened the stream
            tokenStore.Invalidate(token);

            await auditLogger.RecordAsync(
                AuditAction.DataExportDownloaded,
                actorUserId: currentUser.UserId,
                subjectUserId: currentUser.UserId,
                metadata: new Dictionary<string, object?> { ["token_prefix"] = token[..8] },
                cancellationToken);

            return Results.File(stream, "application/zip", "taxreader-export.zip");
        })
        .WithName("DownloadDataExport")
        .WithSummary("Download the prepared data export bundle (one-time)");

        return group;
    }
}
