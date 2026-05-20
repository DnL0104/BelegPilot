using TaxReader.Application.Commands;
using TaxReader.Application.Queries;

namespace TaxReader.Api.Endpoints;

public static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder group)
    {
        var settings = group.MapGroup("/settings")
            .WithTags("Settings");

        settings.MapGet("/", async (
            GetUserSettingsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { error = result.Error });
        })
        .WithName("GetUserSettings")
        .WithSummary("Get the current user's settings");

        settings.MapPut("/", async (
            UpdateUserSettingsRequest request,
            UpdateUserSettingsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateUserSettingsCommand(request.AutoConfirmThreshold);
            var result = await handler.HandleAsync(command, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("UpdateUserSettings")
        .WithSummary("Update the current user's settings");

        return group;
    }
}

public record UpdateUserSettingsRequest(double? AutoConfirmThreshold);
