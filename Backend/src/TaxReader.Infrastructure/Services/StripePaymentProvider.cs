using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.Infrastructure.Services;

public class StripePaymentProvider(
    IOptions<StripeOptions> stripeOptions,
    ILogger<StripePaymentProvider> logger)
    : IStripePaymentProvider
{
    private readonly StripeClient _client = new StripeClient(stripeOptions.Value.SecretKey);

    public async Task<string> CreateCheckoutSessionAsync(
        Guid userId,
        int credits,
        string? stripeCustomerId,
        CancellationToken cancellationToken = default)
    {
        var options = BuildSessionCreateOptions(credits, stripeCustomerId);
        options.Metadata["userId"] = userId.ToString();

        var service = _client.V1.Checkout.Sessions;
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);
        logger.LogInformation("Created Stripe CheckoutSession {SessionId} for User {UserId}, {Credits} credits",
            session.Id, userId, credits);
        return session.Url;
    }

    /// <summary>
    /// Builds the SessionCreateOptions for a checkout session.
    /// Internal to allow unit tests to verify the options shape (PAY-02 KleinunternehmerNote footer)
    /// without making a live Stripe API call.
    /// </summary>
    internal SessionCreateOptions BuildSessionCreateOptions(int credits, string? stripeCustomerId)
    {
        var opts = stripeOptions.Value;
        var pricePack = Array.Find(opts.PricePacks, p => p.Credits == credits);
        if (pricePack is null)
            throw new InvalidOperationException($"Kein Preispaket für {credits} Credits konfiguriert.");

        return new SessionCreateOptions
        {
            Mode = "payment",
            LineItems = [new SessionLineItemOptions { Price = pricePack.StripePriceId, Quantity = 1 }],
            Customer = stripeCustomerId,
            // Pass existing customer ID so email is pre-filled; "always" ensures a customer record
            // exists for invoice and portal association (Pitfall 5 mitigation)
            CustomerCreation = stripeCustomerId is null ? "always" : null,
            Metadata = new Dictionary<string, string>
            {
                { "userId", string.Empty }, // overwritten by caller with actual userId
                { "credits", credits.ToString() }
            },
            SuccessUrl = $"{opts.AppBaseUrl}/billing?payment=success",
            CancelUrl = $"{opts.AppBaseUrl}/billing",
            InvoiceCreation = new SessionInvoiceCreationOptions
            {
                Enabled = true,
                InvoiceData = new SessionInvoiceCreationInvoiceDataOptions
                {
                    // PAY-02: DE-compliant Kleinunternehmer note per §19 UStG
                    Footer = opts.KleinunternehmerNote
                }
            }
        };
    }

    public async Task<string> CreatePortalSessionAsync(
        string stripeCustomerId,
        CancellationToken cancellationToken = default)
    {
        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = stripeCustomerId,
            ReturnUrl = $"{stripeOptions.Value.AppBaseUrl}/billing"
        };
        var service = _client.V1.BillingPortal.Sessions;
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);
        return session.Url;
    }

    public async Task<IReadOnlyList<InvoiceDto>> GetInvoicesAsync(
        string stripeCustomerId,
        CancellationToken cancellationToken = default)
    {
        var options = new InvoiceListOptions
        {
            Customer = stripeCustomerId,
            Limit = 20
        };
        var service = _client.V1.Invoices;
        var invoices = await service.ListAsync(options, cancellationToken: cancellationToken);
        return invoices.Data
            .Select(i => new InvoiceDto(
                i.Id,
                i.Number,
                i.AmountPaid / 100m,
                i.Currency,
                i.Created,
                i.InvoicePdf,
                i.HostedInvoiceUrl))
            .ToList();
    }

    /// <summary>
    /// PAY-02: Expands the Stripe checkout session to retrieve the first line-item price id.
    /// The price id is the authoritative server-side key for credit derivation — it comes
    /// directly from Stripe's verified line-items data and cannot be forged by the client.
    /// Returns null if the session has no line items or the price id cannot be resolved.
    /// </summary>
    public async Task<string?> ExpandSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var service = _client.V1.Checkout.Sessions;
        var expanded = await service.GetAsync(
            sessionId,
            new SessionGetOptions { Expand = ["line_items"] },
            requestOptions: null,
            cancellationToken: cancellationToken);

        return expanded.LineItems?.Data?.FirstOrDefault()?.Price?.Id;
    }
}
