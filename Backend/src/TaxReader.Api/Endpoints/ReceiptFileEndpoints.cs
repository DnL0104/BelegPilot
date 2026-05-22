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

            return result.IsSuccess
                ? Results.Accepted(value: result.Value)
                : Results.UnprocessableEntity(new { error = result.Error });
        })
        .DisableAntiforgery()
        .WithName("UploadReceiptFiles")
        .WithSummary("Upload one or more receipt files — returns 202 Accepted with per-file job IDs for polling");

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

        receiptFiles.MapGet("/{id:guid}/status", async (
            Guid id,
            GetReceiptFileStatusHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetReceiptFileStatusQuery(id), ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound();
        })
        .WithName("GetReceiptFileStatus")
        .WithSummary("Get the current processing status for a receipt file (D-13 polling endpoint)");

        receiptFiles.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelReceiptFileHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new CancelReceiptFileCommand(id), ct);
            if (result.IsSuccess) return Results.NoContent();
            return result.Error switch
            {
                "NotFound" => Results.NotFound(),
                "TerminalState" => Results.Conflict(new
                {
                    error = "Beleg ist bereits fertig verarbeitet — Abbruch nicht möglich.",
                    errorCode = "TerminalState"
                }),
                _ => Results.BadRequest()
            };
        })
        .WithName("CancelReceiptFile")
        .WithSummary("Cancel processing of a receipt file; returns 204 on success, 409 for terminal state, 404 if not found");

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
