namespace TaxReader.Application.DTOs;

public record CheckoutSessionDto(string CheckoutUrl, bool IsDemoMode = false);
public record InvoiceDto(
    string Id,
    string? Number,
    decimal AmountPaid,
    string Currency,
    DateTime Created,
    string? InvoicePdfUrl,
    string? HostedInvoiceUrl);
public record PortalSessionDto(string Url);
public record CreateCheckoutSessionRequest(int Credits, bool WaiverAccepted, bool AgbAccepted);
