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
                stripeOptions.Value.WebhookSecret);
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
            // T-02: idempotency — UNIQUE constraint on stripe_event_id
            var alreadyProcessed = await dbContext.Payments
                .AnyAsync(p => p.StripeEventId == stripeEventId, cancellationToken);
            if (alreadyProcessed)
            {
                // Pitfall 2: always 200 on duplicate — never 4xx (Stripe would retry)
                logger.LogInformation("Duplicate Stripe event {StripeEventId} — ignoring", stripeEventId);
                return Results.Ok();
            }

            if (!session.Metadata.TryGetValue("userId", out var userIdStr) ||
                !Guid.TryParse(userIdStr, out var userId))
            {
                logger.LogWarning("Stripe event {StripeEventId} missing userId metadata", stripeEventId);
                return Results.Ok();
            }

            if (!session.Metadata.TryGetValue("credits", out var creditsStr) ||
                !int.TryParse(creditsStr, out var credits))
            {
                logger.LogWarning("Stripe event {StripeEventId} missing credits metadata", stripeEventId);
                return Results.Ok();
            }

            // Pitfall 5: persist stripe_customer_id on User so repeat buyers don't create duplicate Stripe customers
            if (session.CustomerId is not null)
            {
                var user = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                if (user is not null && user.StripeCustomerId is null)
                {
                    user.StripeCustomerId = session.CustomerId;
                }
            }

            dbContext.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StripeEventId = stripeEventId,
                StripeSessionId = session.Id,
                // StripePaymentIntentId populated for reliable charge.refunded correlation in Plan 05-04
                StripePaymentIntentId = session.PaymentIntentId,
                CreditsGranted = credits,
                AmountCents = (int)(session.AmountTotal ?? 0),
                Currency = session.Currency ?? "eur",
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);

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

            await jobClient.EnqueueAsync<TaxReader.Application.Jobs.RevokeTokensJob>(
                j => j.HandleAsync(matchingPayment.UserId, matchingPayment.CreditsGranted, CancellationToken.None),
                cancellationToken);

            logger.LogInformation(
                "Enqueued RevokeTokensJob for User {UserId}, {Credits} credits, charge.refunded event {StripeEventId}",
                matchingPayment.UserId, matchingPayment.CreditsGranted, stripeEvent.Id);
        }

        return Results.Ok();
    }
}
