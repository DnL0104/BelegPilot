using TaxReader.Application.Commands;
using TaxReader.Application.Queries;
using TaxReader.Domain.Enums;

namespace TaxReader.Api.Endpoints;

public static class ClassificationEndpoints
{
    public static RouteGroupBuilder MapClassificationEndpoints(this RouteGroupBuilder group)
    {
        var classification = group.MapGroup("/receipt-items")
            .WithTags("Classification");

        classification.MapPost("/{id:guid}/confirm", async (
            Guid id,
            ConfirmClassificationRequest request,
            ConfirmClassificationHandler handler,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<Category>(request.Category, true, out var category))
                return Results.BadRequest(new { error = $"Invalid category: {request.Category}" });

            var command = new ConfirmClassificationCommand(id, category);
            var result = await handler.HandleAsync(command, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("ConfirmClassification")
        .WithSummary("Manually confirm a classification for a receipt item");

        classification.MapPost("/batch-confirm", async (
            BatchConfirmRequest request,
            BatchConfirmHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new BatchConfirmCommand(request.ItemIds);
            var result = await handler.HandleAsync(command, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(new { confirmed = result.Value })
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("BatchConfirmClassifications")
        .WithSummary("Confirm AI-suggested classifications for multiple items at once");

        classification.MapGet("/pending-suggestions", async (
            GetPendingSuggestionsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetPendingSuggestionsQuery(), cancellationToken);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetPendingSuggestions")
        .WithSummary("Get all items with pending AI-suggested classifications");

        return group;
    }
}

public record ConfirmClassificationRequest(string Category);
public record BatchConfirmRequest(IReadOnlyList<Guid> ItemIds);
