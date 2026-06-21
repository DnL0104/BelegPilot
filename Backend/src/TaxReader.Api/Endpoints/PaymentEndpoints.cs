using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaxReader.Application.DTOs;
using TaxReader.Application.Interfaces;
using TaxReader.Infrastructure.Configuration;
using TaxReader.Infrastructure.Services;

namespace TaxReader.Api.Endpoints;

public static class PaymentEndpoints
{
    public static RouteGroupBuilder MapPaymentEndpoints(this RouteGroupBuilder group)
    {
        var payments = group.MapGroup("/payments").WithTags("Payments");

        payments.MapPost("/checkout", async (
            CreateCheckoutSessionRequest request,
            ICurrentUser currentUser,
            IStripePaymentProvider stripeProvider,
            ITokenService tokenService,
            IAppDbContext dbContext,
            IOptions<StripeOptions> stripeOptions,
            CancellationToken cancellationToken) =>
        {
            if (!request.WaiverAccepted || !request.AgbAccepted)
                return Results.BadRequest(new { error = "Bitte akzeptieren Sie die AGB und den Widerrufsverzicht." });

            var validCredits = new[] { 100, 500, 1500 };
            if (!validCredits.Contains(request.Credits))
                return Results.BadRequest(new { error = "Ungültige Credits-Anzahl." });

            var opts = stripeOptions.Value;

            // D-14: DemoMode — skip Stripe, credit directly
            if (opts.DemoMode)
            {
                await tokenService.AddTokensAsync(
                    request.Credits,
                    Domain.Enums.TokenTransactionType.Purchase,
                    $"Demo-Kauf: {request.Credits} Credits",
                    cancellationToken);
                return Results.Ok(new CheckoutSessionDto(
                    $"{opts.AppBaseUrl}/billing?payment=success", IsDemoMode: true));
            }

            try
            {
                // T-04 IDOR mitigation + Pitfall 5: read stripe_customer_id scoped to authenticated user.
                // Prevents repeat buyers from creating duplicate Stripe customer records.
                var user = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);

                var session = await stripeProvider.CreateCheckoutSessionAsync(
                    currentUser.UserId, request.Credits, user?.StripeCustomerId, cancellationToken);
                return Results.Ok(new CheckoutSessionDto(session));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateCheckoutSession")
        .WithSummary("Create a Stripe Checkout session for a token pack purchase");

        payments.MapGet("/invoices", async (
            ICurrentUser currentUser,
            IStripePaymentProvider stripeProvider,
            IAppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            // T-04 IDOR mitigation: read stripe_customer_id scoped to authenticated user only
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);

            if (user?.StripeCustomerId is null)
                return Results.Ok(Array.Empty<InvoiceDto>());

            var invoices = await stripeProvider.GetInvoicesAsync(user.StripeCustomerId, cancellationToken);
            return Results.Ok(invoices);
        })
        .WithName("GetInvoices")
        .WithSummary("List Stripe invoices for the current user");

        payments.MapPost("/portal", async (
            ICurrentUser currentUser,
            IStripePaymentProvider stripeProvider,
            IAppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);

            if (user?.StripeCustomerId is null)
                return Results.BadRequest(new { error = "Kein Stripe-Kundenkonto gefunden. Bitte tätigen Sie zunächst einen Kauf." });

            try
            {
                var url = await stripeProvider.CreatePortalSessionAsync(user.StripeCustomerId, cancellationToken);
                return Results.Ok(new PortalSessionDto(url));
            }
            catch (Stripe.StripeException)
            {
                return Results.BadRequest(new { error = "Weiterleitung zum Kundenportal fehlgeschlagen. Bitte versuchen Sie es erneut." });
            }
        })
        .WithName("CreatePortalSession")
        .WithSummary("Create a Stripe Customer Portal session");

        return group;
    }

    /// <summary>
    /// D-15: Webhook endpoint — anonymous, NOT under /api/v1 auth group.
    /// Raw body must be read before any JSON model binding consumes the stream.
    /// Accepts HttpRequest directly so ASP.NET does not consume the body (Pitfall 1).
    /// </summary>
    public static WebApplication MapStripeWebhookEndpoint(this WebApplication app)
    {
        app.MapPost("/webhooks/stripe", async (
            HttpRequest request,
            StripeWebhookHandler handler,
            CancellationToken cancellationToken) =>
        {
            var json = await new System.IO.StreamReader(request.Body).ReadToEndAsync(cancellationToken);
            var sig = request.Headers["Stripe-Signature"].ToString();
            return await handler.HandleAsync(json, sig, cancellationToken);
        })
        .AllowAnonymous()
        .WithTags("Webhooks");

        return app;
    }
}
