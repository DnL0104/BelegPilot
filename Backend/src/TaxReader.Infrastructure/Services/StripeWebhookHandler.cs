using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using TaxReader.Application.Interfaces;
using TaxReader.Application.Jobs;
using TaxReader.Domain.Entities;
using TaxReader.Domain.Enums;
using TaxReader.Infrastructure.Configuration;

namespace TaxReader.Infrastructure.Services;

/// <summary>
/// Handles Stripe webhook events.
/// Registered as Scoped so it can use IAppDbContext and IBackgroundJobClient.
/// The endpoint delegates to this class to keep logic testable outside the HTTP route.
/// </summary>
public class StripeWebhookHandler(
    IOptions<StripeOptions> stripeOptions,
    IAppDbContext dbContext,
    IBackgroundJobClient jobClient,
    IStripePaymentProvider stripeProvider,
    ILogger<StripeWebhookHandler> logger)
{
    public async Task<IResult> HandleAsync(
        string json,
        string? signatureHeader,
        CancellationToken cancellationToken)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                signatureHeader,
                stripeOptions.Value.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            logger.LogWarning("Stripe webhook signature validation failed: {Message}", ex.Message);
            return Results.BadRequest();
        }

        if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
        {
            var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
            if (session is null) return Results.Ok();

            var stripeEventId = stripeEvent.Id;

            if (!session.Metadata.TryGetValue("userId", out var userIdStr) ||
                !Guid.TryParse(userIdStr, out var userId))
            {
                logger.LogWarning("Stripe event {StripeEventId} missing userId metadata", stripeEventId);
                return Results.Ok();
            }

            // PAY-02: derive credits server-side from the verified Stripe line-item price id.
            // Never trust session.Metadata["credits"] — it is client-visible and can be forged (T-01-10).
            var priceId = await stripeProvider.ExpandSessionAsync(session.Id, cancellationToken);
            var pricePack = stripeOptions.Value.PricePacks
                .FirstOrDefault(p => p.StripePriceId == priceId);

            if (pricePack is null)
            {
                logger.LogWarning(
                    "Stripe event {StripeEventId}: no PricePack matches priceId {PriceId} — ignoring",
                    stripeEventId, priceId);
                return Results.Ok(); // Always 200 — Stripe must not retry
            }

            var credits = pricePack.Credits;

            // PAY-03: atomic idempotent insert via IAppDbContext.InsertPaymentAtomicAsync.
            // ON CONFLICT (stripe_event_id) DO NOTHING returns 0 on duplicate; 1 on first insert.
            // Eliminates the race window in the AnyAsync → Add → SaveChanges two-step.
            var rows = await dbContext.InsertPaymentAtomicAsync(
                userId,
                stripeEventId,
                session.Id,
                session.PaymentIntentId,
                credits,
                (int)(session.AmountTotal ?? 0),
                session.Currency ?? "eur",
                cancellationToken);

            if (rows == 0)
            {
                logger.LogInformation("Duplicate Stripe event {StripeEventId} — ignoring", stripeEventId);
                return Results.Ok();
            }

            // rows == 1: new insert succeeded. Update StripeCustomerId and enqueue grant job.

            // Pitfall 5: persist stripe_customer_id on User so repeat buyers don't create duplicate Stripe customers
            if (session.CustomerId is not null)
            {
                var user = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                if (user is not null && user.StripeCustomerId is null)
                {
                    user.StripeCustomerId = session.CustomerId;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            await jobClient.EnqueueAsync<GrantTokensJob>(
                j => j.HandleAsync(userId, credits, CancellationToken.None),
                cancellationToken);

            logger.LogInformation(
                "Processed checkout.session.completed for User {UserId}, {Credits} credits, event {StripeEventId}",
                userId, credits, stripeEventId);
        }
        else if (stripeEvent.Type == EventTypes.ChargeRefunded)
        {
            var charge = stripeEvent.Data.Object as Stripe.Charge;
            if (charge is null) return Results.Ok();

            // Correlate via StripePaymentIntentId — reliable even when user has multiple
            // same-priced purchases (AmountCents matching would be ambiguous in that case).
            var paymentIntentId = charge.PaymentIntentId;
            if (string.IsNullOrEmpty(paymentIntentId))
            {
                logger.LogWarning(
                    "charge.refunded event {StripeEventId}: no PaymentIntentId on charge — cannot correlate",
                    stripeEvent.Id);
                return Results.Ok();
            }

            var matchingPayment = await dbContext.Payments
                .Where(p => p.StripePaymentIntentId == paymentIntentId && p.Status == TaxReader.Domain.Enums.PaymentStatus.Granted)
                .FirstOrDefaultAsync(cancellationToken);

            if (matchingPayment is null)
            {
                logger.LogWarning(
                    "charge.refunded event {StripeEventId}: no Granted payment found for PaymentIntentId {PaymentIntentId}",
                    stripeEvent.Id, paymentIntentId);
                return Results.Ok();
            }

            // D-04: UserId is nullable after GDPR anonymization. If the user was deleted, the
            // payment row is retained with user_id = NULL. Skip token revocation — there is no
            // user balance to revoke against; the account and its tokens are already gone.
            if (matchingPayment.UserId is null)
            {
                logger.LogInformation(
                    "charge.refunded event {StripeEventId}: payment {PaymentId} has no user (GDPR-deleted) — skipping revocation",
                    stripeEvent.Id, matchingPayment.Id);
                return Results.Ok();
            }

            await jobClient.EnqueueAsync<TaxReader.Application.Jobs.RevokeTokensJob>(
                j => j.HandleAsync(matchingPayment.UserId.Value, matchingPayment.CreditsGranted, CancellationToken.None),
                cancellationToken);

            logger.LogInformation(
                "Enqueued RevokeTokensJob for User {UserId}, {Credits} credits, charge.refunded event {StripeEventId}",
                matchingPayment.UserId, matchingPayment.CreditsGranted, stripeEvent.Id);
        }

        return Results.Ok();
    }
}
