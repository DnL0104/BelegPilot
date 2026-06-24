using Microsoft.AspNetCore.Mvc;
using TaxReader.Application.Commands;
using TaxReader.Application.Queries;

namespace TaxReader.Api.Endpoints;

public static class ReceiptEndpoints
{
    public static RouteGroupBuilder MapReceiptEndpoints(this RouteGroupBuilder group)
    {
        var receipts = group.MapGroup("/receipts")
            .WithTags("Receipts");

        receipts.MapGet("/", async (
            int? year,
            GetReceiptsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetReceiptsQuery(year), cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetReceipts")
        .WithSummary("List all parsed receipts, optionally filtered by year");

        receipts.MapGet("/{id:guid}", async (
            Guid id,
            GetReceiptByIdHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetReceiptByIdQuery(id), cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("GetReceiptById")
        .WithSummary("Get a single receipt by ID");

        receipts.MapGet("/{id:guid}/items", async (
            Guid id,
            GetReceiptItemsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetReceiptItemsQuery(id), cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("GetReceiptItems")
        .WithSummary("Get all items for a receipt");

        receipts.MapPost("/{id:guid}/reclassify", async (
            Guid id,
            [FromServices] ReclassifyReceiptHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new ReclassifyReceiptCommand(id), cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("ReclassifyReceipt")
        .WithSummary("Re-run AI classification on all items of a receipt");

        receipts.MapPost("/{id:guid}/retry-failed", async (
            Guid id,
            [FromServices] RetryFailedItemsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new RetryFailedItemsCommand(id), cancellationToken);

            if (result.IsSuccess)
                return Results.Ok(result.Value);

            // CR-02: only the IDOR not-found case is a true 404. "Receipt exists but has no
            // failed items" is a client error against an existing resource → 422, so the UI and
            // retry logic don't misread it as "receipt gone".
            return result.Error!.Contains("not found", StringComparison.Ordinal)
                ? Results.NotFound(new { error = result.Error })
                : Results.UnprocessableEntity(new { error = result.Error });
        })
        .WithName("RetryFailedClassifications")
        .WithSummary("Retry AI classification for items that failed with a technical error");

        receipts.MapPost("/{id:guid}/acknowledge-sum", async (
            Guid id,
            AcknowledgeSumMismatchHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new AcknowledgeSumMismatchCommand(id), cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("AcknowledgeSumMismatch")
        .WithSummary("Dismiss the sum-mismatch warning on a receipt");

        return group;
    }
}
