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
}
