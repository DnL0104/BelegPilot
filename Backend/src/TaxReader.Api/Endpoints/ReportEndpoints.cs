using TaxReader.Application.Queries;
using TaxReader.Infrastructure.Services;

namespace TaxReader.Api.Endpoints;

public static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder group)
    {
        var reports = group.MapGroup("/reports")
            .WithTags("Reports");

        reports.MapGet("/category-totals", async (
            int year,
            GetCategoryTotalsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetCategoryTotalsQuery(year), cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetCategoryTotals")
        .WithSummary("Get category totals for a given year");

        reports.MapGet("/annual-summary", async (
            int year,
            GetAnnualSummaryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new GetAnnualSummaryQuery(year), cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("GetAnnualSummary")
        .WithSummary("Get annual summary including category breakdown for a given year");

        reports.MapGet("/export/csv", async (
            int year,
            bool? confirmedOnly,
            GetExportDataHandler handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetExportDataQuery(year, confirmedOnly ?? false);
            var result = await handler.HandleAsync(query, cancellationToken);

            if (!result.IsSuccess)
                return Results.BadRequest(new { error = result.Error });

            var csv = CsvExportService.Generate(result.Value!);
            return Results.File(csv, "text/csv; charset=utf-8",
                $"TaxReader_Export_{year}.csv");
        })
        .WithName("ExportCsv")
        .WithSummary("Export classified items as CSV for a given year");

        reports.MapGet("/export/pdf", async (
            int year,
            bool? confirmedOnly,
            GetExportDataHandler handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetExportDataQuery(year, confirmedOnly ?? false);
            var result = await handler.HandleAsync(query, cancellationToken);

            if (!result.IsSuccess)
                return Results.BadRequest(new { error = result.Error });

            var pdf = PdfExportService.Generate(year, result.Value!);
            return Results.File(pdf, "application/pdf",
                $"TaxReader_Export_{year}.pdf");
        })
        .WithName("ExportPdf")
        .WithSummary("Export classified items as PDF summary for a given year");

        return group;
    }
}
