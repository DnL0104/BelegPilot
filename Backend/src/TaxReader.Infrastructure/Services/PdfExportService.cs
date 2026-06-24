using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TaxReader.Application.DTOs;

namespace TaxReader.Infrastructure.Services;

public static class PdfExportService
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    private static readonly Dictionary<string, string> CategoryLabels = new()
    {
        ["Unbekannt"]                              = "Nicht zugeordnet",
        ["WerbungskostenArbeitsmittel"]            = "Werbungskosten – Arbeitsmittel",
        ["WerbungskostenFachliteratur"]            = "Werbungskosten – Fachliteratur",
        ["WerbungskostenBueromaterial"]            = "Werbungskosten – Büromaterial",
        ["WerbungskostenReisekosten"]              = "Werbungskosten – Reisekosten",
        ["WerbungskostenFortbildung"]              = "Werbungskosten – Fortbildung",
        ["WerbungskostenTelekommunikation"]        = "Werbungskosten – Telekommunikation",
        ["SonderausgabenSpenden"]                  = "Sonderausgaben – Spenden",
        ["SonderausgabenVorsorgeaufwendungen"]     = "Sonderausgaben – Vorsorgeaufwendungen",
        ["AussergewoehnlicheBelastungenKrankheit"] = "Außergewöhnliche Belastungen – Krankheit",
        ["HaushaltsnaheDienstleistung"]            = "Haushaltsnahe Dienstleistung",
        ["Handwerkerleistung"]                     = "Handwerkerleistung",
        ["Privat"]                                 = "Privat",
    };

    public static byte[] Generate(int year, IReadOnlyList<ExportItemDto> items)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var grouped = items
            .GroupBy(i => CategoryLabels.GetValueOrDefault(i.Category, i.Category))
            .OrderBy(g => g.Key)
            .ToList();

        var totalAmount = items.Sum(i => i.TotalPrice);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(50);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text($"Steuerrelevante Ausgaben {year}")
                        .FontSize(18).Bold();
                    col.Item().PaddingTop(4).Text($"Erstellt am {DateTime.Now:dd.MM.yyyy}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(12).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(16).Column(content =>
                {
                    // Summary box
                    content.Item().Background(Colors.Grey.Lighten4).Padding(16).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Gesamtbetrag").FontSize(9).FontColor(Colors.Grey.Medium);
                            c.Item().Text(totalAmount.ToString("C", De)).FontSize(16).Bold();
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Artikel gesamt").FontSize(9).FontColor(Colors.Grey.Medium);
                            c.Item().Text(items.Count.ToString()).FontSize(16).Bold();
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Kategorien").FontSize(9).FontColor(Colors.Grey.Medium);
                            c.Item().Text(grouped.Count.ToString()).FontSize(16).Bold();
                        });
                    });

                    content.Item().PaddingTop(20);

                    // Category sections
                    foreach (var group in grouped)
                    {
                        var catTotal = group.Sum(i => i.TotalPrice);

                        content.Item().Column(catCol =>
                        {
                            // Category header
                            catCol.Item().Background("#f0fdf4").Padding(10).Row(headerRow =>
                            {
                                headerRow.RelativeItem().Text(group.Key)
                                    .FontSize(11).Bold();
                                headerRow.AutoItem().Text($"{catTotal.ToString("C", De)} ({group.Count()} Artikel)")
                                    .FontSize(10).FontColor(Colors.Grey.Darken1);
                            });

                            // Items table
                            catCol.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(65);  // Datum
                                    cols.ConstantColumn(80);  // Anbieter
                                    cols.RelativeColumn();     // Beschreibung
                                    cols.ConstantColumn(35);  // Menge
                                    cols.ConstantColumn(65);  // Einzelpreis
                                    cols.ConstantColumn(70);  // Gesamt
                                });

                                // Table header
                                table.Header(header =>
                                {
                                    var headerStyle = TextStyle.Default.FontSize(8).FontColor(Colors.Grey.Medium);
                                    header.Cell().PaddingVertical(6).PaddingLeft(4).Text("Datum").Style(headerStyle);
                                    header.Cell().PaddingVertical(6).Text("Anbieter").Style(headerStyle);
                                    header.Cell().PaddingVertical(6).Text("Beschreibung").Style(headerStyle);
                                    header.Cell().PaddingVertical(6).AlignRight().Text("Menge").Style(headerStyle);
                                    header.Cell().PaddingVertical(6).AlignRight().Text("Einzelpreis").Style(headerStyle);
                                    header.Cell().PaddingVertical(6).PaddingRight(4).AlignRight().Text("Gesamt").Style(headerStyle);

                                    header.Cell().ColumnSpan(6)
                                        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
                                });

                                // Rows
                                var rowIndex = 0;
                                foreach (var item in group.OrderBy(i => i.PurchaseDate))
                                {
                                    var bg = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                                    table.Cell().Background(bg).PaddingVertical(5).PaddingLeft(4)
                                        .Text(item.PurchaseDate.ToString("dd.MM.yyyy")).FontSize(8);
                                    table.Cell().Background(bg).PaddingVertical(5)
                                        .Text(item.Vendor).FontSize(8);
                                    table.Cell().Background(bg).PaddingVertical(5)
                                        .Text(item.Description).FontSize(8);
                                    table.Cell().Background(bg).PaddingVertical(5).AlignRight()
                                        .Text(item.Quantity.ToString()).FontSize(8);
                                    table.Cell().Background(bg).PaddingVertical(5).AlignRight()
                                        .Text(item.UnitPrice.ToString("N2", De)).FontSize(8);
                                    table.Cell().Background(bg).PaddingVertical(5).PaddingRight(4).AlignRight()
                                        .Text(item.TotalPrice.ToString("N2", De)).FontSize(8).Bold();

                                    rowIndex++;
                                }
                            });

                            catCol.Item().PaddingTop(14);
                        });
                    }
                });

                // D-09: per-page footer with StBerG §5-safe disclaimer — QuestPDF renders Footer on every page automatically.
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Vorschlag – keine Steuerberatung (§ 5 StBerG)  •  Seite ")
                        .FontSize(7).FontColor(Colors.Grey.Medium);
                    x.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
                    x.Span(" von ").FontSize(7).FontColor(Colors.Grey.Medium);
                    x.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
                    x.Span($"  •  TaxReader Export {year}").FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }
}
