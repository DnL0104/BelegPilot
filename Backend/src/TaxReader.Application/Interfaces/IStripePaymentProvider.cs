using TaxReader.Application.DTOs;

namespace TaxReader.Application.Interfaces;

public interface IStripePaymentProvider
{
    Task<string> CreateCheckoutSessionAsync(
        Guid userId,
        int credits,
        string? stripeCustomerId,
        CancellationToken cancellationToken = default);

    Task<string> CreatePortalSessionAsync(
        string stripeCustomerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceDto>> GetInvoicesAsync(
        string stripeCustomerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// PAY-02: Expands the Stripe checkout session to retrieve the first line-item price id.
    /// Used by the webhook handler to derive credits server-side from verified Stripe data
    /// instead of trusting session.Metadata["credits"] (which is client-influenceable).
    /// Returns null if the session has no line items or the price id cannot be resolved.
    /// </summary>
    Task<string?> ExpandSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}
