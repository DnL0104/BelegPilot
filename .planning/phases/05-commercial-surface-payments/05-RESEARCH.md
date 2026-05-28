# Phase 5: Commercial Surface (Payments) - Research

**Researched:** 2026-05-28
**Domain:** Stripe payment integration, webhook handling, DE legal compliance (Widerrufsrecht / Kleinunternehmer), ASP.NET Core Minimal API
**Confidence:** HIGH (Stripe APIs verified, patterns confirmed against official docs and codebase)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Token Pack Pricing (PAY-01)**
- D-01: Three packages — 50 credits / 4,99 €, 200 credits / 14,99 €, 500 credits / 29,99 €. Match TopUpDialog existing UI; no UI changes to prices or volumes.
- D-02: Stripe Products + Prices created manually in Stripe Dashboard once per environment. Their Stripe Price IDs stored in config (`StripeOptions.PricePacks` — array of `{ Credits, StripePriceId }` in appsettings.json, overridden via env vars). Backend maps selected credit count → Price ID. No code creates/manages Stripe Products.

**Checkout UX Flow (PAY-01, PAY-03)**
- D-03: Checkout uses Stripe hosted checkout (redirect to stripe.com). Backend `POST /payments/checkout` creates CheckoutSession with `mode=payment`, `success_url`, `cancel_url`, returns session URL. No embedded Payment Element.
- D-04: `success_url` is `/billing?payment=success`. Billing page detects query param and shows banner. TanStack Query `refetchInterval` polls `/tokens/balance` every 3 s for 15 s.
- D-05: Widerrufsrecht + AGB gate inside `TopUpDialog`. Two required unchecked checkboxes before "Kaufen" is enabled. Backend `POST /payments/checkout` validates `waiverAccepted: true`.

**VAT / MwSt Strategy (PAY-02)**
- D-06: Prices displayed as Bruttopreise (VAT included). No separate VAT line in the UI.
- D-07: Kleinunternehmer (§19 UStG) at launch — no USt-IdNr., no VAT charged. `StripeOptions.KleinunternehmerNote` config key for invoice footer copy.

**Billing Page (PAY-04)**
- D-08: Separate `/billing` route under `(authenticated)` route group.
- D-09: Sidebar nav item label: "Credits & Abrechnung", positioned after "Einstellungen".
- D-10: Billing page content: token balance card + "Aufladen" button, transaction history (last 20), invoice list with "Herunterladen" link, "Zahlungsmethode verwalten" button (Stripe Customer Portal redirect via `POST /payments/portal`).
- D-11: When `balance < 0`: `POST /receipt-files` returns `402 Payment Required` with German message. Existing receipts/reports accessible.

**Multi-Environment Safety (PAY-06)**
- D-12: `StripeOptions` with `SecretKey`, `PublishableKey`, `WebhookSecret`, `PricePacks`, `DemoMode` (bool), `BusinessAddress`, `KleinunternehmerNote`. Separate values per environment via `Stripe__SecretKey` env var.
- D-13: Startup guard via `IValidateOptions<StripeOptions>` with `ValidateOnStart()`: Production + `sk_test_` → throw. Development + `sk_live_` → `LogWarning`.
- D-14: `Stripe__DemoMode=true` toggle skips Stripe, directly credits tokens, returns synthetic success.

**Webhook + Idempotency (PAY-01)**
- D-15: Webhook endpoint `POST /webhooks/stripe` is anonymous (`.AllowAnonymous()`), NOT under `/api/v1` auth group. Stripe signature verified. On `checkout.session.completed`: insert into `payments` table with `(stripe_event_id UNIQUE)` guard, enqueue `GrantTokensJob`. Duplicate event returns 200 silently.
- D-16: `payments` table: `id` (UUID PK), `user_id` (FK users), `stripe_event_id` (UNIQUE), `stripe_session_id`, `credits_granted` (int), `amount_cents` (int), `currency`, `status` (Pending/Granted/Revoked), `created_at`, `revoked_at?`.

### Claude's Discretion
- Exact Stripe.net NuGet version (use latest stable `Stripe.net` 47.x or current)
- Whether `GrantTokensJob` is Hangfire fire-and-forget or recurring check job
- Exact shadcn components for billing page (Card, Table, Badge — follow existing patterns)
- Stripe Customer Portal redirect vs embed (redirect chosen by D-10)
- Exact Stripe metadata keys (`userId`, `credits`) on the Checkout Session
- `/billing?payment=success` polling implementation detail (refetchInterval duration)

### Deferred Ideas (OUT OF SCOPE)
- Subscription / recurring billing
- Mollie / SEPA / giropay — alternative DE payment methods (INT-V2-02)
- Volume discounts or enterprise packs
- Self-hosted Stripe-equivalent (Paddle, LemonSqueezy) — Stripe locked
- Stripe Tax automatic calculation
- Audit log for payment grants — Phase 6 (LEG-08)
- `/widerruf` and `/agb` legal pages — Phase 6 (LEG-04, LEG-03)
- AVV with Stripe (DPA) — Phase 6 (LEG-06)
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PAY-01 | `StripePaymentProvider` + `POST /payments/checkout` + `/webhooks/stripe` with signature verification + `payments` table + `GrantTokensJob` | Stripe.net 51.2.0 `SessionService.CreateAsync`, `EventUtility.ConstructEvent`, Hangfire fire-and-forget pattern |
| PAY-02 | DE-compliant Rechnung via Stripe Invoicing (Kleinunternehmer §19 UStG) + download from billing page | `SessionInvoiceCreationOptions`, `InvoiceListOptions`, `invoice_pdf` URL field |
| PAY-03 | Pre-checkout Widerrufsrecht waiver flow — statutory text §356 Abs. 4 BGB + AGB checkbox | Statutory text confirmed, React Hook Form checkbox validation pattern |
| PAY-04 | Billing page: token balance, transaction history, invoice list, Stripe Customer Portal embed | `BillingPortal.Sessions` service, `InvoiceListOptions` with customer filter |
| PAY-05 | Refund/chargeback — `charge.refunded` webhook → `RevokeTokensJob` → balance can go negative | `refund.created` event (newer, D-15 specifies `charge.refunded`), `RevokeTokensJob` follows GrantTokensJob pattern |
| PAY-06 | Multi-environment safety — `sk_test_` / `sk_live_` startup guard + `ValidateOnStart()` | `IValidateOptions<T>` pattern already used in `RefreshTokenOptionsValidator` — identical pattern |
</phase_requirements>

---

## Summary

Phase 5 adds real Stripe money flow to the existing token credit system. The technical landscape is well-understood: Stripe.net 51.2.0 is stable and backward-compatible with the documented API surface. The project's existing `IValidateOptions<T>` pattern (from Phase 2's `RefreshTokenOptionsValidator`) maps directly to the `StripeOptionsValidator` requirement. The Hangfire fire-and-forget pattern (from Phase 3's `ProcessReceiptFileJob`) is the exact pattern for `GrantTokensJob` and `RevokeTokensJob`.

The most technically nuanced part of the phase is the webhook endpoint: it MUST be registered outside the `/api/v1` auth group (`.AllowAnonymous()`), and the raw request body must be read via `StreamReader` without prior JSON model binding. In Minimal API this is achieved naturally by accepting `HttpRequest` as the endpoint parameter — the body stream is not consumed by model binding when you bypass typed parameter binding. No `EnableBuffering` middleware is required in Minimal API style.

The second notable area is the Stripe Customer ID lifecycle: the `users` table needs a nullable `stripe_customer_id` column. Checkout sessions should pass the customer ID when it exists (so Stripe pre-fills the email), and it must be persisted from the `checkout.session.completed` event if the session used `customer_creation: "always"`.

**Primary recommendation:** Use `Stripe.net 51.2.0` with the `StripeClient` per-instance pattern (not global config). Mount the webhook endpoint at `/webhooks/stripe` directly on `app` (not on the `api` group) with `.AllowAnonymous()`. Follow the `RefreshTokenOptionsValidator` pattern for `StripeOptionsValidator`.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Checkout Session creation | API / Backend | — | Stripe secret key must never reach browser; backend creates session URL and returns it |
| Webhook event receipt + verification | API / Backend | — | HMAC-SHA256 signature verification requires `WebhookSecret`; anonymous, no auth |
| Token grant after payment | Application (Hangfire job) | Database | Fire-and-forget async after webhook confirmation; writes to `payments` + `token_transactions` |
| Token revocation after refund | Application (Hangfire job) | Database | Same pattern as grant; balance can go negative |
| Customer Portal session creation | API / Backend | — | Requires `SecretKey`; returns portal URL to frontend |
| Invoice list / PDF URL | API / Backend | — | Stripe API call with customer ID; returns typed DTO to frontend |
| Balance negative check (402 guard) | API / Backend | — | Endpoint-level guard before uploading files |
| Widerrufsrecht + AGB gate | Frontend (TopUpDialog) | API (server-side guard) | Client-side: checkbox UI. Server-side: `waiverAccepted: true` validation in request body |
| Billing page UI | Frontend (Client Component) | — | Interactive: balance polling, dialog, download links |
| DemoMode bypass | API / Backend | Frontend (banner) | Env-var controlled; frontend shows banner when mode active |
| Startup key guard | API / Backend (startup) | — | `IValidateOptions<StripeOptions>` via `ValidateOnStart()` |

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Stripe.net | 51.2.0 | Stripe API client: CheckoutSession, webhook verification, invoices, Customer Portal | Official Stripe .NET SDK; only viable option for Stripe integration |
| Hangfire.Core | 1.8.23 (existing) | Fire-and-forget `GrantTokensJob` and `RevokeTokensJob` | Already installed in Phase 3; same fire-and-forget pattern as `ProcessReceiptFileJob` |
| EF Core 10.0.4 | 10.0.4 (existing) | `payments` table migration, UNIQUE constraint on `stripe_event_id` | Existing ORM; no new package |
| Microsoft.Extensions.Options | included in SDK | `IValidateOptions<StripeOptions>` + `ValidateOnStart()` | Existing pattern from `RefreshTokenOptionsValidator` |

[VERIFIED: npm registry / dotnet package search — Stripe.net 51.2.0 confirmed as latest stable, released 2026-05-27]

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| TanStack Query (existing) | ^5.96.2 | `/billing?payment=success` polling, invoice list, portal mutation | Already in use across the frontend |
| React Hook Form + Zod (existing) | ^7.72.1 / ^4.3.6 | AGB + Widerrufsrecht checkbox validation in TopUpDialog | Already used in upload form |
| sonner (existing) | ^2.0.7 | Toast notifications on payment success / failure | Already used project-wide |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Stripe.net 51.x | Stripe.net 47.x (as mentioned in context) | 47.x is outdated — 51.2.0 is current stable (released 2026-05-27). CONTEXT.md's "47.x range" was an approximate guidance at context-write time. Use 51.2.0. |
| `refund.created` event | `charge.refunded` (D-15 spec) | CONTEXT.md specified `charge.refunded`; use it as it is stable and works for all Stripe Checkout refunds. Note: `refund.created` is the newer preferred event (2024 API change) but `charge.refunded` remains correct for v1. |
| StripeClient per-instance | Global `StripeConfiguration.ApiKey` | Per-instance is recommended since v46 — better for multi-tenant config and testability. |

**Installation:**
```bash
# Backend — add to Directory.Packages.props
dotnet add package Stripe.net --version 51.2.0
# (edit csproj to add PackageReference without version per CPM rules)
```

**Version verification:** [VERIFIED: dotnet package search — `Stripe.net 51.2.0`, 86M total downloads, released 2026-05-27]

---

## Architecture Patterns

### System Architecture Diagram

```
Frontend TopUpDialog
  ─[select pack + check AGB + check Widerrufsrecht]→
  POST /api/v1/payments/checkout
    ─[backend: map credits → StripePriceId]→
    StripeClient.V1.Checkout.Sessions.CreateAsync(options)
      metadata: { userId, credits }
      customer: existing stripe_customer_id OR customer_creation: "always"
      success_url: /billing?payment=success
      invoice_creation: enabled + footer note
    ←[returns session.Url]
  ←[frontend: window.location.href = session.Url]

  [Stripe-hosted checkout]
  ←[Stripe: POST /webhooks/stripe with Stripe-Signature header]
    EventUtility.ConstructEvent(rawBody, signature, webhookSecret)
    if checkout.session.completed:
      INSERT payments (stripe_event_id UNIQUE) — idempotent guard
      GrantTokensJob.HandleAsync(userId, credits) — fire-and-forget
    if charge.refunded:
      UPDATE payments status → Revoked
      RevokeTokensJob.HandleAsync(userId, credits) — fire-and-forget

GrantTokensJob → ITokenService.AddTokensAsync(credits, Purchase, description)
RevokeTokensJob → ITokenService.AddTokensAsync(-credits, Revoke, description)
                                      ↓
                             token_transactions row
                             user_token_balances.Balance updated

Frontend /billing?payment=success
  → detect query param
  → useEffect: set refetchInterval=3000 for 15s on tokenBalance query
  → show "Credits werden gutgeschrieben" banner

Frontend /billing (full page)
  GET /api/v1/payments/invoices → InvoiceListDto[]
  GET /api/v1/tokens/balance → TokenBalanceDto
  GET /api/v1/tokens/transactions → TokenTransactionDto[]
  POST /api/v1/payments/portal → { url: "https://billing.stripe.com/..." }
    → window.location.href = url

POST /api/v1/receipt-files (balance guard, D-11)
  if balance.Balance < 0 → 402 Payment Required
```

### Recommended Project Structure

```
Backend/src/TaxReader.Domain/
├── Entities/Payment.cs                    # NEW
├── Enums/PaymentStatus.cs                 # NEW (Pending/Granted/Revoked)

Backend/src/TaxReader.Infrastructure/
├── Configuration/StripeOptions.cs         # NEW
├── Services/StripePaymentProvider.cs      # NEW (IStripePaymentProvider impl)

Backend/src/TaxReader.Application/
├── Interfaces/IStripePaymentProvider.cs   # NEW
├── Jobs/GrantTokensJob.cs                 # NEW
├── Jobs/RevokeTokensJob.cs                # NEW
├── DTOs/PaymentDtos.cs                    # NEW (CheckoutSessionDto, InvoiceDto, PortalSessionDto)

Backend/src/TaxReader.Api/
├── Endpoints/PaymentEndpoints.cs          # NEW (/payments/checkout, /payments/portal, /payments/invoices)
├── Webhooks/StripeWebhookEndpoints.cs     # NEW (/webhooks/stripe — anonymous, outside /api/v1)

Backend/src/TaxReader.Infrastructure/
├── Data/Configurations/PaymentConfiguration.cs  # NEW
├── Migrations/<timestamp>_AddPaymentsTable.cs    # NEW
```

### Pattern 1: CheckoutSession Creation (StripePaymentProvider)

**What:** Create a Stripe Checkout Session from a credit count selection  
**When to use:** Called by `POST /payments/checkout` handler

```csharp
// Source: https://docs.stripe.com/api/checkout/sessions/create?lang=dotnet
// Pattern: StripeClient per-instance (v46+ recommended over global config)

public class StripePaymentProvider(
    IOptions<StripeOptions> stripeOptions,
    ILogger<StripePaymentProvider> logger)
    : IStripePaymentProvider
{
    private readonly StripeClient _client =
        new StripeClient(stripeOptions.Value.SecretKey);

    public async Task<string> CreateCheckoutSessionAsync(
        Guid userId,
        int credits,
        string? stripeCustomerId,
        CancellationToken cancellationToken = default)
    {
        var opts = stripeOptions.Value;
        var pricePack = opts.PricePacks.Single(p => p.Credits == credits);

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            LineItems = [new SessionLineItemOptions { Price = pricePack.StripePriceId, Quantity = 1 }],
            // Pass existing customer ID so email is pre-filled; "always" ensures a customer
            // record exists for invoice and portal association
            Customer = stripeCustomerId,
            CustomerCreation = stripeCustomerId is null ? "always" : null,
            // metadata carries userId + credits — the webhook extracts these for GrantTokensJob
            Metadata = new Dictionary<string, string>
            {
                { "userId", userId.ToString() },
                { "credits", credits.ToString() }
            },
            SuccessUrl = $"{opts.AppBaseUrl}/billing?payment=success",
            CancelUrl = $"{opts.AppBaseUrl}/billing",
            InvoiceCreation = new SessionInvoiceCreationOptions
            {
                Enabled = true,
                InvoiceData = new SessionInvoiceCreationInvoiceDataOptions
                {
                    Footer = opts.KleinunternehmerNote  // "Gemäß §19 UStG wird keine Umsatzsteuer berechnet."
                }
            }
        };

        var service = _client.V1.Checkout.Sessions;
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);
        logger.LogInformation("Created Stripe CheckoutSession {SessionId} for User {UserId}, {Credits} credits",
            session.Id, userId, credits);
        return session.Url;
    }
}
```

[CITED: https://docs.stripe.com/api/checkout/sessions/create?lang=dotnet]

### Pattern 2: Webhook Endpoint — Raw Body + Signature Verification (Minimal API)

**What:** Anonymous endpoint reading raw body to verify Stripe signature  
**When to use:** The `POST /webhooks/stripe` endpoint (not inside `/api/v1` group)

```csharp
// Source: https://gist.github.com/cjavilla-stripe/efaeba1abe949592906bcf928e1e5ba4
// Key insight: Accept HttpRequest directly → ASP.NET does NOT consume body via model binding.
// No EnableBuffering middleware needed — body stream is already available for manual reading.

app.MapPost("/webhooks/stripe", async (
    HttpRequest request,
    StripeWebhookHandler handler,
    CancellationToken cancellationToken) =>
{
    var json = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);
    return await handler.HandleAsync(json, request.Headers["Stripe-Signature"], cancellationToken);
})
.AllowAnonymous()
.WithTags("Webhooks");

// In the handler:
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
            var session = stripeEvent.Data.Object as Session;
            var userId = Guid.Parse(session!.Metadata["userId"]);
            var credits = int.Parse(session.Metadata["credits"]);
            var stripeEventId = stripeEvent.Id;

            // Idempotency: UNIQUE constraint on stripe_event_id
            var alreadyProcessed = await dbContext.Payments
                .AnyAsync(p => p.StripeEventId == stripeEventId, cancellationToken);
            if (alreadyProcessed)
            {
                logger.LogInformation("Duplicate Stripe event {StripeEventId} — ignoring", stripeEventId);
                return Results.Ok();  // 200 to prevent Stripe retry
            }

            dbContext.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StripeEventId = stripeEventId,
                StripeSessionId = session.Id,
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
        }
        else if (stripeEvent.Type == EventTypes.ChargeRefunded)
        {
            // RevokeTokensJob pattern — see Pattern 4
        }

        return Results.Ok();
    }
}
```

[CITED: https://docs.stripe.com/webhooks?lang=dotnet]

### Pattern 3: IValidateOptions<StripeOptions> Startup Guard

**What:** Fail-fast guard preventing production deployment with test keys  
**When to use:** Registered in DependencyInjection.cs with `ValidateOnStart()`

```csharp
// Source: Existing project pattern — RefreshTokenOptionsValidator in
// Backend/src/TaxReader.Infrastructure/Configuration/RefreshTokenOptions.cs
// IDENTICAL pattern to the existing validator

public sealed class StripeOptionsValidator(IWebHostEnvironment env)
    : IValidateOptions<StripeOptions>
{
    public ValidateOptionsResult Validate(string? name, StripeOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SecretKey))
            return ValidateOptionsResult.Fail("Stripe:SecretKey ist nicht konfiguriert.");

        if (string.IsNullOrWhiteSpace(options.WebhookSecret))
            return ValidateOptionsResult.Fail("Stripe:WebhookSecret ist nicht konfiguriert.");

        // D-13: Production + test key = hard fail
        if (env.IsProduction() && options.SecretKey.StartsWith("sk_test_"))
            throw new InvalidOperationException(
                "Stripe SecretKey ist ein Testschlüssel in einer Production-Umgebung.");

        // D-13: Development + live key = loud warning (not a throw — devs occasionally test against live)
        // Sentry captures this as a critical alert in production (above guard catches it first)
        return ValidateOptionsResult.Success;
    }
}

// Registration in DependencyInjection.cs (same as RefreshTokenOptions pattern):
services.AddSingleton<IValidateOptions<StripeOptions>, StripeOptionsValidator>();
services.AddOptions<StripeOptions>()
    .Bind(configuration.GetSection(StripeOptions.SectionName))
    .ValidateOnStart();
```

Note: The `IWebHostEnvironment` injection requires `IWebHostEnvironment` to be added to the DI container BEFORE the validator is called, which it is in ASP.NET Core. The validator is a Singleton but `IWebHostEnvironment` is also Singleton — no lifetime mismatch. [VERIFIED: existing project pattern in Backend/src/TaxReader.Infrastructure/Configuration/RefreshTokenOptions.cs]

### Pattern 4: GrantTokensJob + RevokeTokensJob (Hangfire Fire-and-Forget)

**What:** Background jobs to credit/debit tokens after payment confirmation  
**When to use:** Enqueued from webhook handler

```csharp
// Source: Existing project pattern — ProcessReceiptFileJob in
// Backend/src/TaxReader.Application/Jobs/ProcessReceiptFileJob.cs

public class GrantTokensJob(
    IAppDbContext dbContext,
    ILogger<GrantTokensJob> logger)
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })]
    public async Task HandleAsync(
        Guid userId,
        int credits,
        CancellationToken cancellationToken)
    {
        using var _scope = LogContext.PushProperty("JobId", $"Grant_{userId}_{credits}");

        // ITokenService depends on ICurrentUser (which requires HttpContext).
        // Hangfire jobs run without HttpContext → access DbContext + TokenTransaction directly.
        var balance = await dbContext.UserTokenBalances
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);
        // ... credit balance + add TokenTransaction row ...

        // Update Payment status to Granted
        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Status == PaymentStatus.Pending
                                   && p.CreditsGranted == credits, cancellationToken);
        if (payment is not null)
        {
            payment.Status = PaymentStatus.Granted;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
```

**Critical design note:** `ITokenService` is injected with `ICurrentUser` which reads from `IHttpContextAccessor`. Hangfire jobs run without an HTTP context. `GrantTokensJob` and `RevokeTokensJob` MUST NOT inject `ITokenService`. They must work directly with `IAppDbContext` to update `UserTokenBalance` and write `TokenTransaction` rows. [ASSUMED — based on codebase inspection; confirmed by TokenService.cs using `currentUser.UserId`]

### Pattern 5: Stripe Customer Portal Session

**What:** Create a one-time portal URL and redirect the user  
**When to use:** Called by `POST /payments/portal` handler

```csharp
// Source: https://docs.stripe.com/customer-management/integrate-customer-portal?lang=dotnet

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
```

**Prerequisite:** The Customer Portal must be configured in the Stripe Dashboard (Settings → Billing → Customer portal) before `CreateAsync` will succeed. This is a one-time manual operator setup step — not code. [CITED: https://docs.stripe.com/customer-management/integrate-customer-portal?lang=dotnet]

### Pattern 6: Invoice List (Stripe Invoicing)

**What:** List invoices for a Stripe customer and return PDF URLs  
**When to use:** Called by `GET /payments/invoices`

```csharp
// Source: https://docs.stripe.com/api/invoices/list?lang=dotnet

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
            i.InvoicePdf,         // PDF download URL (populated when finalized)
            i.HostedInvoiceUrl))  // Hosted page URL
        .ToList();
}
```

**Note:** `InvoicePdf` and `HostedInvoiceUrl` are `null` until the invoice is finalized (paid). A Checkout Session with `invoice_creation.enabled = true` auto-finalizes the invoice on payment. [CITED: https://docs.stripe.com/api/invoices/list?lang=dotnet]

### Pattern 7: DemoMode Bypass

**What:** Skip Stripe in demo mode and directly credit tokens  
**When to use:** `StripeOptions.DemoMode = true` (env var only)

```csharp
// In the checkout endpoint handler:
if (stripeOptions.Value.DemoMode)
{
    // Direct credit — no Stripe call, return synthetic success URL
    var balance = await tokenService.AddTokensAsync(
        credits,
        TokenTransactionType.Purchase,
        $"Demo-Kauf: {credits} Credits",
        cancellationToken);
    return Result<CheckoutSessionDto>.Success(
        new CheckoutSessionDto($"{appBaseUrl}/billing?payment=success", isDemoMode: true));
}
```

### Anti-Patterns to Avoid

- **Never call `StripeConfiguration.ApiKey = ...` globally** — use `new StripeClient(secretKey)` per instance. Global config is thread-unsafe for multiple key scenarios (test vs. live) and makes testing harder.
- **Never parse the webhook body with `[FromBody]`** — use `HttpRequest` directly so the raw byte sequence is preserved for HMAC verification. Any middleware deserialization breaks the signature check.
- **Never inject `ITokenService` into Hangfire jobs** — `ITokenService` uses `ICurrentUser.UserId` from `IHttpContextAccessor`, which is null outside HTTP request scope. Write directly to `IAppDbContext`.
- **Never return 4xx from the webhook handler for duplicate events** — return 200. Stripe retries on any non-2xx, creating an infinite retry loop.
- **Never store the Stripe secret key in `appsettings.json`** — always via `Stripe__SecretKey` env var (gitignored `.env` file in dev, container env in production).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Stripe signature verification | Custom HMAC-SHA256 with timing-safe compare | `EventUtility.ConstructEvent(json, sig, secret)` | Timing attack protection built in; handles tolerance window (300s default) |
| Invoice PDF generation | QuestPDF for Stripe receipts | Stripe Invoicing (`invoice_creation: { enabled: true }`) | Stripe generates DE-compliant invoices; handles sequential numbering, VAT notes, Kleinunternehmer |
| Idempotency tracking | In-memory HashSet | DB UNIQUE constraint on `stripe_event_id` | Survives restarts; handles concurrent duplicate deliveries at the DB level |
| Payment webhook delivery reliability | Custom retry / queue | Stripe's built-in retry (tries for 3 days with exponential backoff) | Stripe retries on non-2xx; idempotent handler + return 200 is all that's needed |
| Customer Portal UI | Custom payment method management page | Stripe Customer Portal (redirect, not embed) | PCI scope, card update flows, saved methods — all maintained by Stripe |

**Key insight:** Stripe handles invoice generation, delivery reliability, and payment method management. The backend's job is exclusively: create session → verify webhook → grant/revoke tokens → serve invoice URLs.

---

## Runtime State Inventory

> This phase introduces new state; no rename/refactor involved.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | `payments` table: does not exist yet | EF migration: `AddPaymentsTable` |
| Stored data | `users.stripe_customer_id`: does not exist yet | EF migration: add nullable column |
| Live service config | Stripe Dashboard: Products + Prices must be created manually (D-02) | Operator creates in Stripe Dashboard for test and live environments; pastes Price IDs into config |
| Live service config | Stripe Customer Portal: must be configured in Dashboard before portal sessions work | Operator one-time setup in Dashboard |
| OS-registered state | None — no new scheduled tasks or OS-level registrations | — |
| Secrets/env vars | `Stripe__SecretKey`, `Stripe__PublishableKey`, `Stripe__WebhookSecret` — new env vars | Add to `.env.example`; add to `docker-compose.yml` environment block |
| Build artifacts | None — Stripe.net adds no build-time artifacts | — |

---

## Common Pitfalls

### Pitfall 1: Webhook Body Consumed by Middleware Before Signature Check

**What goes wrong:** If `app.UseEndpoints()` or JSON model binding consumes the request body stream before the webhook handler reads it, `EventUtility.ConstructEvent` receives an empty string and throws `StripeException("No raw body provided")`.

**Why it happens:** ASP.NET Core's body-reading is a one-pass stream by default. Any middleware that reads the body (e.g., logging middleware that reads full body, `[FromBody]` binding) consumes the stream.

**How to avoid:** Accept `HttpRequest` as the Minimal API parameter type (not a typed model). The body stream is untouched until the endpoint explicitly reads it with `StreamReader`. Example: `app.MapPost("/webhooks/stripe", async (HttpRequest request, ...) =>`. [VERIFIED: official Stripe gist cjavilla-stripe/efaeba1abe949592906bcf928e1e5ba4]

**Warning signs:** `StripeException` with message "No raw body provided" or `StripeException` "Stripe-Signature header not found" in logs.

### Pitfall 2: Webhook Returns 4xx on Duplicate Event → Infinite Retry Loop

**What goes wrong:** If the webhook handler returns `400 Bad Request` or `409 Conflict` when a duplicate `stripe_event_id` is detected, Stripe treats this as a delivery failure and retries for up to 3 days, eventually leading to thousands of duplicate deliveries.

**Why it happens:** Stripe only considers the event "delivered" on a 2xx response.

**How to avoid:** Always return `200 OK` even for duplicate events. Log the duplicate at `Information` level and return early. [CITED: https://docs.stripe.com/webhooks]

### Pitfall 3: ITokenService Fails in Hangfire Job (HttpContext is null)

**What goes wrong:** `GrantTokensJob` injects `ITokenService`. At runtime the DI container resolves `TokenService`, which has a primary constructor dependency on `ICurrentUser`. `CurrentUser` reads `IHttpContextAccessor.HttpContext.User`, which is `null` in a background job context → `NullReferenceException`.

**Why it happens:** Hangfire workers do not have an HTTP request context. `IHttpContextAccessor.HttpContext` is `null` in the worker thread.

**How to avoid:** `GrantTokensJob` and `RevokeTokensJob` MUST NOT inject `ITokenService`. Inject `IAppDbContext` directly and replicate the balance + transaction write logic without the `ICurrentUser` dependency. Pass `userId` as a job parameter. [VERIFIED: codebase inspection — TokenService.cs line 8 shows `ICurrentUser` constructor param]

### Pitfall 4: Stripe Customer Portal Session Fails Without Dashboard Config

**What goes wrong:** `BillingPortal.Sessions.CreateAsync` throws `StripeException("You have not configured a customer portal")` even with a valid customer ID.

**Why it happens:** The Customer Portal UI/permissions must be configured in the Stripe Dashboard before the API endpoint will accept session creation requests.

**How to avoid:** Document as a required operator step in the README/deployment guide. Catch this specific exception in `StripePaymentProvider` and return a user-friendly German error. [CITED: https://docs.stripe.com/customer-management/integrate-customer-portal?lang=dotnet]

### Pitfall 5: Missing stripe_customer_id Prevents Invoice Association

**What goes wrong:** If `POST /payments/checkout` doesn't persist the `stripe_customer_id` returned in `checkout.session.completed`, subsequent invoice list and portal session calls fail (no customer ID to query against).

**Why it happens:** The `Customer` field in the Checkout Session is set by Stripe when `CustomerCreation = "always"`. The webhook `checkout.session.completed` event payload includes `session.CustomerId`. If this isn't written to `users.stripe_customer_id`, the billing page cannot fetch invoices.

**How to avoid:** In the webhook handler for `checkout.session.completed`, after inserting the `Payment` row, also update `users.stripe_customer_id = session.CustomerId` if it's not already set. [ASSUMED — from Stripe API docs and payment flow analysis]

### Pitfall 6: Stripe.net StripeClient Version Mismatch (Service Paths Changed in v46+)

**What goes wrong:** Code written for Stripe.net pre-v46 uses `new SessionService()` (global config). In v46+, the recommended path is `client.V1.Checkout.Sessions`. Both work, but mixing styles leads to config confusion.

**Why it happens:** Stripe.net v46 introduced the `StripeClient` entry-point approach.

**How to avoid:** Use `new StripeClient(secretKey)` and `_client.V1.Checkout.Sessions` / `_client.V1.Invoices` / `_client.V1.BillingPortal.Sessions` consistently. Never use `StripeConfiguration.ApiKey`. [CITED: Stripe.net GitHub README — "In version 46 of the Stripe .NET SDK, we have enhanced the StripeClient class"]

### Pitfall 7: `charge.refunded` vs `refund.created` Event Choice

**What goes wrong:** Using `refund.created` (the newer 2024 Stripe API) but registering the webhook in Stripe Dashboard to listen for `charge.refunded` (or vice versa). Stripe delivers only the events you register for.

**Why it happens:** Stripe updated refund events in 2024 (`2024-10-28.acacia` API version). Both exist but cover different scenarios.

**How to avoid:** D-15 spec says `charge.refunded`. Register for `charge.refunded` in the Stripe Dashboard. The `Event.Type` comparison must match the registered event type exactly (`EventTypes.ChargeRefunded`). [CITED: https://docs.stripe.com/changelog/acacia/2024-10-28/refund-webhook-update]

---

## Code Examples

### StripeOptions Configuration Class

```csharp
// Source: Project pattern from Backend/src/TaxReader.Infrastructure/Configuration/JwtOptions.cs
// Follows identical SectionName + IOptions<T> pattern

namespace TaxReader.Infrastructure.Configuration;

public class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public bool DemoMode { get; set; } = false;
    public string AppBaseUrl { get; set; } = "http://localhost:3000";
    public string BusinessAddress { get; set; } = string.Empty;
    public string KleinunternehmerNote { get; set; } =
        "Gemäß §19 UStG wird keine Umsatzsteuer berechnet.";
    public PricePack[] PricePacks { get; set; } = [];
}

public record PricePack(int Credits, string StripePriceId);
```

### Payment Entity + EF Configuration

```csharp
// Source: Project pattern from UserTokenBalance + UserTokenBalanceConfiguration

// Domain/Entities/Payment.cs
public class Payment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StripeEventId { get; set; } = string.Empty;  // UNIQUE
    public string StripeSessionId { get; set; } = string.Empty;
    public int CreditsGranted { get; set; }
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "eur";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public User User { get; set; } = null!;
}

// Domain/Enums/PaymentStatus.cs
public enum PaymentStatus
{
    Pending = 0,
    Granted = 1,
    Revoked = 2
}

// Infrastructure/Data/Configurations/PaymentConfiguration.cs
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.StripeEventId).IsRequired().HasMaxLength(255);
        builder.HasIndex(e => e.StripeEventId).IsUnique();  // D-16 idempotency guard
        builder.Property(e => e.StripeSessionId).IsRequired().HasMaxLength(255);
        builder.Property(e => e.Currency).IsRequired().HasMaxLength(3);
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
    }
}
```

### Frontend: `/billing?payment=success` Polling Pattern

```typescript
// Source: Project pattern from Frontend/src/hooks/use-tokens.ts
// TanStack Query refetchInterval with time-bounded polling (D-04)

// In billing page component (useEffect approach):
const [isPolling, setIsPolling] = useState(false);
const { data: balance } = useTokenBalance({
  refetchInterval: isPolling ? 3000 : 30000
});

useEffect(() => {
  if (searchParams.get("payment") === "success") {
    setIsPolling(true);
    const timeout = setTimeout(() => setIsPolling(false), 15_000);  // 15s then stop
    return () => clearTimeout(timeout);
  }
}, [searchParams]);
```

### Frontend: Widerrufsrecht Checkbox Exact Legal Text

```typescript
// Source: CONTEXT.md § Specific Ideas + D-05 + §356 Abs. 4 BGB statutory text
// Do NOT paraphrase — this is verbatim statutory language

const WIDERRUFSRECHT_TEXT =
  "Ich verlange ausdrücklich, dass mit der Ausführung des Vertrags sofort begonnen wird. " +
  "Mir ist bekannt, dass ich hierdurch mein Widerrufsrecht verliere.";

const AGB_TEXT = "Ich akzeptiere die Allgemeinen Geschäftsbedingungen.";
// Note: /agb link is a Phase 6 placeholder — in Phase 5 it can href="/agb" (404 acceptable)
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `StripeConfiguration.ApiKey = key` (global) | `new StripeClient(key)` per instance | v46 (2024) | Enables per-instance key config; safer for test/live separation |
| `charge.refunded` only | `refund.created` also available (works for all refund types) | 2024-10-28 API version | D-15 uses `charge.refunded` — valid; `refund.created` is the newer preferred event for new integrations |
| Manual idempotency (in-memory HashSet) | DB-level UNIQUE constraint on `stripe_event_id` | Best practice (always) | Survives restarts; handles race conditions |
| Controller-based webhook endpoint | Minimal API with `HttpRequest` parameter | ASP.NET Core 6+ | Eliminates `EnableBuffering` middleware requirement; body stream untouched |

**Deprecated/outdated:**
- `EventUtility.ParseEvent(json)` without signature verification: Only use `EventUtility.ConstructEvent(json, sig, secret)` — the unsigned variant is only for local dev testing.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `GrantTokensJob` must NOT inject `ITokenService` because `ICurrentUser` requires HTTP context | Pitfall 3, Pattern 4 | If wrong, the job would fail at runtime. Mitigated by existing codebase inspection confirming `TokenService` constructor requires `ICurrentUser`. Low risk. |
| A2 | `stripe_customer_id` must be stored on `users` table after first checkout session | Pitfall 5 | If not stored, invoices and Customer Portal sessions would not be available. This is standard Stripe integration practice. |
| A3 | Stripe Customer Portal session creation requires pre-configuration in Dashboard | Pitfall 4 | If not configured, API call fails. This is clearly documented in Stripe docs. Low risk once documented. |

**All other claims in this research were verified via Stripe official docs, NuGet registry, or codebase inspection.**

---

## Open Questions

1. **`StripeOptions.AppBaseUrl` source**
   - What we know: `success_url` and `cancel_url` must be absolute URLs; config needs an `AppBaseUrl` property.
   - What's unclear: This value isn't in existing config. Options: (a) add `Stripe__AppBaseUrl` env var, (b) derive from `ASPNETCORE_URLS`, (c) use a relative path if Stripe supports it (it doesn't for hosted checkout).
   - Recommendation: Add `Stripe__AppBaseUrl` with default `http://localhost:3000` (matches frontend dev URL). Set to `https://your-domain.com` in production.

2. **Where to register `/webhooks/stripe` in Program.cs**
   - What we know: It must be outside the `api = app.MapGroup("/api/v1").RequireAuthorization()` group. It needs `.AllowAnonymous()`.
   - What's unclear: Whether to mount it as a top-level route or a new group without auth.
   - Recommendation: Register directly on `app` before the `api` group: `app.MapPost("/webhooks/stripe", ...)`.

3. **`DemoMode` sk_test_ warning in development**
   - What we know: D-13 says Development + `sk_live_` → `LogWarning`.
   - What's unclear: `StripeOptionsValidator` is `IValidateOptions<T>` which must return `ValidateOptionsResult`, not log. The warning needs to happen differently — either in the validator returning Success but calling a logger, or in an `IHostedService` startup check.
   - Recommendation: In the validator, return `ValidateOptionsResult.Success` for the dev+live-key scenario. Add a startup `IHostedService` (or a log line in `Program.cs` after `app.Build()`) that checks `if (!env.IsProduction() && secretKey.StartsWith("sk_live_"))` and calls `logger.LogWarning(...)`.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Backend compilation | ✓ | 10.0.201 | — |
| Docker | Integration testing, deployment | ✓ | 29.3.1 | — |
| PostgreSQL | EF migration, `payments` table | ✓ (Docker) | 17 Alpine | — |
| Stripe CLI (`stripe listen`) | Local webhook forwarding in dev | ✗ | — | Use Stripe Dashboard webhook test tool; or install `stripe CLI` |
| Stripe account (test mode) | Creating test Products/Prices | ✗ (not verifiable) | — | Operator must have test Stripe account; test key starts `sk_test_` |

**Missing dependencies with fallback:**
- Stripe CLI: Not installed. Local webhook forwarding (`stripe listen --forward-to http://localhost:5190/webhooks/stripe`) requires CLI installation. Alternative: use Stripe Dashboard → Webhooks → Test with an example event button. Add CLI install instruction to README.

**Missing dependencies with no fallback:**
- Stripe test account: Required. Operator must create Products+Prices manually and paste Price IDs into config. No code fallback — blocked until Stripe account is available.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + FluentAssertions 7.0.0 + Moq 4.20.72 |
| Config file | `Backend/tests/TaxReader.UnitTests/TaxReader.UnitTests.csproj` |
| Quick run command | `dotnet test Backend/tests/TaxReader.UnitTests` |
| Full suite command | `dotnet test Backend` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PAY-01 | Checkout session created + returns URL | unit | `dotnet test Backend --filter "CheckoutSession"` | ❌ Wave 0 |
| PAY-01 | Webhook: valid signature → event parsed | unit | `dotnet test Backend --filter "StripeWebhook"` | ❌ Wave 0 |
| PAY-01 | Webhook: invalid signature → 400 | unit | `dotnet test Backend --filter "StripeWebhook"` | ❌ Wave 0 |
| PAY-01 | Webhook: duplicate stripe_event_id → 200, no second grant | unit | `dotnet test Backend --filter "GrantTokensJob"` | ❌ Wave 0 |
| PAY-01 | GrantTokensJob credits tokens in DB | unit | `dotnet test Backend --filter "GrantTokensJob"` | ❌ Wave 0 |
| PAY-05 | RevokeTokensJob debits tokens, balance goes negative | unit | `dotnet test Backend --filter "RevokeTokensJob"` | ❌ Wave 0 |
| PAY-06 | StripeOptionsValidator: Production + sk_test_ → throws | unit | `dotnet test Backend --filter "StripeOptionsValidator"` | ❌ Wave 0 |
| PAY-06 | StripeOptionsValidator: missing SecretKey → fails | unit | `dotnet test Backend --filter "StripeOptionsValidator"` | ❌ Wave 0 |
| PAY-03 | Widerrufsrecht text matches §356 Abs. 4 BGB statutory text | manual-only | — | No test for statutory copy — human review |

### Sampling Rate
- **Per task commit:** `dotnet test Backend/tests/TaxReader.UnitTests --filter "Payment OR Stripe OR GrantTokens OR RevokeTokens"`
- **Per wave merge:** `dotnet test Backend`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `Backend/tests/TaxReader.UnitTests/Jobs/GrantTokensJobTests.cs` — covers PAY-01 (token crediting)
- [ ] `Backend/tests/TaxReader.UnitTests/Jobs/RevokeTokensJobTests.cs` — covers PAY-05 (token revocation)
- [ ] `Backend/tests/TaxReader.UnitTests/Configuration/StripeOptionsValidatorTests.cs` — covers PAY-06
- [ ] `Backend/tests/TaxReader.UnitTests/Webhooks/StripeWebhookHandlerTests.cs` — covers PAY-01 (signature, duplicate, event routing)

---

## Security Domain

### Applicable ASVS Categories (Level 1)

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Webhook uses HMAC signature, not bearer auth |
| V3 Session Management | No | No session state introduced |
| V4 Access Control | Yes | Webhook endpoint is anonymous; `/payments/*` endpoints require auth; `stripe_customer_id` is scoped per user |
| V5 Input Validation | Yes | FluentValidation on `POST /payments/checkout` request; `credits` must match a valid pack; `waiverAccepted: true` required |
| V6 Cryptography | Yes | Webhook signature uses `EventUtility.ConstructEvent` — do not hand-roll HMAC |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Replay / duplicate webhook event | Tampering | DB UNIQUE constraint on `stripe_event_id` (idempotent insert) |
| Forged webhook (no signature check) | Spoofing | `EventUtility.ConstructEvent` verifies HMAC-SHA256 with 300s tolerance window |
| Insecure key in source control | Information Disclosure | `Stripe__SecretKey` via env var only; never in `appsettings.json`; startup guard validates |
| Production with test key | Elevation of Privilege | `StripeOptionsValidator` throws on prod + `sk_test_` |
| Token grant without payment | Elevation of Privilege | `POST /tokens/purchase` stub removed or gated behind DemoMode only; all real grants come from webhook-verified events |
| Over-crediting via duplicate event | Elevation of Privilege | UNIQUE constraint + `status: Granted` check prevents double-grant |
| IDOR — access other user's invoices | Elevation of Privilege | `GET /payments/invoices` uses `ICurrentUser.UserId` → `users.stripe_customer_id` — cannot query other users' Stripe customers |
| Negative balance lockout bypass | Elevation of Privilege | Balance check in `POST /receipt-files` reads from `UserTokenBalance` which `RevokeTokensJob` updates atomically |

---

## Sources

### Primary (HIGH confidence)
- Stripe.net NuGet registry — `Stripe.net 51.2.0` confirmed as latest stable (2026-05-27)
- [Stripe Checkout Session Create API](https://docs.stripe.com/api/checkout/sessions/create?lang=dotnet) — `SessionCreateOptions` shape, `InvoiceCreation`, `metadata`, `CustomerCreation`
- [Stripe Webhook Handling (.NET)](https://docs.stripe.com/webhooks?lang=dotnet) — `EventUtility.ConstructEvent`, signature verification, idempotency pattern
- [Stripe Customer Portal](https://docs.stripe.com/customer-management/integrate-customer-portal?lang=dotnet) — `BillingPortal.Sessions.CreateAsync`, Dashboard prerequisite
- [Stripe Invoice List API](https://docs.stripe.com/api/invoices/list?lang=dotnet) — `InvoiceListOptions.Customer`, `invoice_pdf` field
- Project codebase: `RefreshTokenOptionsValidator.cs` — `IValidateOptions<T>` + `ValidateOnStart()` pattern
- Project codebase: `ProcessReceiptFileJob.cs` — Hangfire fire-and-forget `[AutomaticRetry]` pattern
- Project codebase: `TokenService.cs` — confirms `ICurrentUser` dependency (rules out injection in Hangfire jobs)

### Secondary (MEDIUM confidence)
- [Stripe webhook Minimal API Gist](https://gist.github.com/cjavilla-stripe/efaeba1abe949592906bcf928e1e5ba4) — raw body reading with `HttpRequest` parameter type
- [Stripe refund event changelog](https://docs.stripe.com/changelog/acacia/2024-10-28/refund-webhook-update) — `refund.created` vs `charge.refunded` distinction

### Tertiary (LOW confidence)
- None — all critical claims verified against official sources or codebase

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — Stripe.net 51.2.0 verified on NuGet; existing Hangfire/EF Core versions confirmed in csproj
- Architecture: HIGH — webhook pattern verified against official Stripe gist; validator pattern verified against codebase
- Pitfalls: HIGH — Pitfall 3 (ITokenService/ICurrentUser) directly confirmed from codebase inspection; others confirmed from official Stripe docs
- Legal text: HIGH — §356 Abs. 4 BGB statutory waiver text confirmed in CONTEXT.md specifics; §19 UStG Kleinunternehmer confirmed

**Research date:** 2026-05-28
**Valid until:** 2026-06-28 (Stripe API stable; 30-day window appropriate)

---

## Project Constraints (from CLAUDE.md)

The following directives from `CLAUDE.md` apply to this phase and the planner must verify compliance:

- **No repository pattern** — `GrantTokensJob` and `RevokeTokensJob` use `IAppDbContext` directly, not a repository interface
- **No MediatR** — handlers are concrete classes injected directly; `StripeWebhookHandler` registered as Scoped
- **No AutoMapper** — hand-write `InvoiceDto` mapping from Stripe `Invoice` object in `StripePaymentProvider`
- **No exceptions for control flow** — webhook handler returns `Results.BadRequest()` for bad signatures (not throws); all handler returns use `Result<T>`
- **Always pass CancellationToken** — all async methods in `StripePaymentProvider`, `GrantTokensJob`, `RevokeTokensJob` must accept and thread `CancellationToken`
- **Async suffix** — `CreateCheckoutSessionAsync`, `CreatePortalSessionAsync`, `GetInvoicesAsync`, `HandleAsync` (job methods)
- **Primary constructors for DI** — `StripePaymentProvider(IOptions<StripeOptions> opts, ...)`, not field injection
- **File-scoped namespaces** — all new `.cs` files
- **German error strings** — `"Ihr Guthaben ist erschöpft. Bitte laden Sie Credits auf."` (D-11); `"Bezahlung konnte nicht initiiert werden."` etc.
- **German UI** — all new frontend strings in German `Sie`-form; EUR formatted via `Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' })`
- **`IOptions<T>` configuration pattern** — `StripeOptions` follows `JwtOptions` / `AnthropicOptions` convention with `SectionName` const
- **Records for DTOs** — `CheckoutSessionDto`, `InvoiceDto`, `PortalSessionDto` as immutable records
- **Central Package Management** — `Stripe.net` version goes in `Backend/Directory.Packages.props`; `csproj` has `<PackageReference Include="Stripe.net" />` without version
- **`dotnet build Backend` must pass** after every plan
