using TaxReader.Application.Commands;
using TaxReader.Application.DTOs;
using TaxReader.Application.Queries;

namespace TaxReader.Api.Endpoints;

public static class ReceiptFileEndpoints
{
    public static RouteGroupBuilder MapReceiptFileEndpoints(this RouteGroupBuilder group)
    {
        var receiptFiles = group.MapGroup("/receipt-files")
            .WithTags("Receipt Files");

        receiptFiles.MapPost("/", async (
            IFormFileCollection files,
            string? sourceHint,
            int? yearHint,
            string? uploadedBy,
            UploadReceiptFilesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var fileItems = files.Select(f => new FileUploadItem(
                f.FileName,
                f.Length,
                f.OpenReadStream())).ToList();

            var command = new UploadReceiptFilesCommand(fileItems, sourceHint, yearHint, uploadedBy);
            var result = await handler.HandleAsync(command, cancellationToken);

            if (result.IsFailure)
                return Results.BadRequest(new { error = result.Error });

            var response = result.Value!;

            // Partial success (or full success): 201 Created with per-file results.
            // The client inspects `successful` / `failed` arrays to render outcomes.
            if (response.Successful.Count > 0)
                return Results.Created("/api/v1/receipts", response);

            // No successes. Differentiate pure-duplicates (409) from other errors (400)
            // so the frontend can show a specific message.
            if (response.Failed.All(f => f.Kind == FailureKind.Duplicate))
                return Results.Conflict(response);

            return Results.BadRequest(response);
        })
        .DisableAntiforgery()
        .RequireRateLimiting("upload-concurrency")
        .WithName("UploadReceiptFiles")
        .WithSummary("Upload and process one or more receipt files (PDF, JPG, PNG, WEBP)");

        receiptFiles.MapGet("/", async (
            GetReceiptFilesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetReceiptFilesQuery(), cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetReceiptFiles")
        .WithSummary("List all uploaded receipt files");

        receiptFiles.MapDelete("/{id:guid}", async (
            Guid id,
            DeleteReceiptFileHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new DeleteReceiptFileCommand(id), cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("DeleteReceiptFile")
        .WithSummary("Delete a receipt file and all associated data");

        receiptFiles.MapPost("/bulk-delete", async (
            BulkDeleteRequest request,
            BulkDeleteReceiptFilesHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new BulkDeleteReceiptFilesCommand(request.Ids);
            var result = await handler.HandleAsync(command, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(new { deleted = result.Value })
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("BulkDeleteReceiptFiles")
        .WithSummary("Delete multiple receipt files and all associated data");

        return group;
    }
}

public record BulkDeleteRequest(IReadOnlyList<Guid> Ids);
