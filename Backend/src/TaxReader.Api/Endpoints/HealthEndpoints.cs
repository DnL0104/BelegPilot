using TaxReader.Application.Interfaces;

namespace TaxReader.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        // OBS-03: liveness probe — DB ping only; anonymous for BetterStack.
        app.MapGet("/health", async (
            IAppDbContext dbContext,
            CancellationToken ct) =>
        {
            var dbUp = await dbContext.Database.CanConnectAsync(ct);
            var status = dbUp ? "healthy" : "unhealthy";
            return Results.Json(
                new { status, db = dbUp ? "up" : "down" },
                statusCode: dbUp ? 200 : 503);
        })
        .AllowAnonymous()
        .WithName("HealthLiveness")
        .WithTags("Health");

        // OBS-03: readiness probe — DB ping + Anthropic-configured check; anonymous for BetterStack.
        // Anthropic not-configured is reported but does NOT fail liveness (it degrades classification
        // only, not the whole service). Body contains only coarse status — no secrets or stack traces.
        app.MapGet("/api/v1/health", async (
            IAppDbContext dbContext,
            IAiClassifier aiClassifier,
            CancellationToken ct) =>
        {
            var dbUp = await dbContext.Database.CanConnectAsync(ct);
            var anthropicConfigured = aiClassifier.IsConfigured;
            var healthy = dbUp;
            return Results.Json(
                new
                {
                    status = healthy ? "healthy" : "unhealthy",
                    db = dbUp ? "up" : "down",
                    anthropic = anthropicConfigured ? "configured" : "unconfigured"
                },
                statusCode: healthy ? 200 : 503);
        })
        .AllowAnonymous()
        .WithName("HealthReadiness")
        .WithTags("Health");
    }
}
