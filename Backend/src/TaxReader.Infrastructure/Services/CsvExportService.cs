using System.Globalization;
using System.Text;
using TaxReader.Application.DTOs;

namespace TaxReader.Infrastructure.Services;

public static class CsvExportService
{
    private static readonly string[] Headers =
    [
        "Datum",
        "Anbieter",
        "Beschreibung",
        "Menge",
        "Einzelpreis",
        "Gesamtpreis",
        "Kategorie",
        "Status",
        "Methode",
        "Begründung"
    ];

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

    private static readonly Dictionary<string, string> StatusLabels = new()
    {
        ["Confirmed"] = "Bestätigt",
        ["Suggested"] = "Vorschlag",
        ["None"] = "Keine",
    };

    public static byte[] Generate(IReadOnlyList<ExportItemDto> items)
    {
        var sb = new StringBuilder();

        // BOM for Excel UTF-8 detection \u2014 MUST be the first bytes of the file, otherwise
        // the umlauts in the disclaimer/data are decoded as mojibake (CR-01).
        sb.Append('\uFEFF');

        // D-08: StBerG \u00A75-safe disclaimer \u2014 must appear before data rows so it is visible
        // when the file is opened in a text editor or spreadsheet application.
        sb.AppendLine("# Steuerliche Vorschl\u00E4ge \u2013 keine Steuerberatung gem\u00E4\u00DF \u00A7 5 StBerG");
        sb.AppendLine("# TaxReader ist ein Hilfsmittel. Alle Klassifizierungen sind Vorschl\u00E4ge ohne Gew\u00E4hr.");
        sb.AppendLine("# Die aufgef\u00FChrten Betr\u00E4ge ersetzen keine Beratung durch einen zugelassenen Steuerberater.");

        // Header
        sb.AppendLine(string.Join(";", Headers));

        // Rows
        foreach (var item in items)
        {
            sb.Append(item.PurchaseDate.ToString("dd.MM.yyyy"));
            sb.Append(';');
            sb.Append(Escape(item.Vendor));
            sb.Append(';');
            sb.Append(Escape(item.Description));
            sb.Append(';');
            sb.Append(item.Quantity);
            sb.Append(';');
            sb.Append(item.UnitPrice.ToString("F2", CultureInfo.GetCultureInfo("de-DE")));
            sb.Append(';');
            sb.Append(item.TotalPrice.ToString("F2", CultureInfo.GetCultureInfo("de-DE")));
            sb.Append(';');
            sb.Append(CategoryLabels.GetValueOrDefault(item.Category, item.Category));
            sb.Append(';');
            sb.Append(StatusLabels.GetValueOrDefault(item.ClassificationStatus, item.ClassificationStatus));
            sb.Append(';');
            sb.Append(item.ClassificationMethod);
            sb.Append(';');
            sb.Append(Escape(item.Reason ?? ""));
            sb.AppendLine();
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Escape(string value)
    {
        // CR-02: neutralize spreadsheet formula injection. Vendor/description/reason come
        // from OCR'd user-uploaded receipts; a value starting with a formula trigger is
        // executed by Excel/LibreOffice/Sheets when the export is opened. Prefix with a
        // single quote (OWASP-recommended) before CSV quoting.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;

        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
