# Ops Setup — BetterStack + Sentry Wiring Instructions

**Requirement:** OBS-03 / QA-06 / D-08
**Audience:** Operator (solo developer). These are external dashboard actions — Claude cannot
automate them because no public provisioning API is assumed. If you later provide a
`BETTERSTACK_API_TOKEN`, an API-based script can create monitors, but that is out of scope
for this plan.

---

## 1. BetterStack — Uptime Monitors

### Why keyword monitors (not status-code-only)

A plain HTTP monitor that only checks for a `200` status will report **Up** even when the
database is down — if the endpoint returns `200` before hitting the DB. The health endpoints
at `/health` and `/api/v1/health` return a JSON body whose `status` field is `"healthy"` only
when all checked components are up. A **keyword monitor** that asserts the body contains
`"healthy"` will flip to **Down** the moment the DB or Anthropic config is missing — catching
real degradation, not just TCP connectivity. (RESEARCH Pitfall 6.)

### Step 1.1 — Create the `/health` monitor

1. Go to **BetterStack → Uptime → Monitors → New monitor**.
2. Set:
   - **Monitor type:** `Keyword monitor`
   - **URL:** `https://<your-domain>/health`
   - **Keyword:** `healthy`
   - **Check frequency:** 1 minute (or 3 minutes for low-traffic; adjust to taste)
   - **Regions:** choose at least 2 EU regions (Frankfurt + Amsterdam recommended)
3. Save. Wait for the first probe to return. Confirm the monitor shows **Up**.

### Step 1.2 — Create the `/api/v1/health` monitor

1. Go to **BetterStack → Uptime → Monitors → New monitor**.
2. Set:
   - **Monitor type:** `Keyword monitor`
   - **URL:** `https://<your-domain>/api/v1/health`
   - **Keyword:** `healthy`
   - **Check frequency:** 1 minute
   - **Regions:** same regions as above
3. Save. Confirm the monitor shows **Up**.

> Both endpoints are anonymous (`.AllowAnonymous()` in the API — no JWT needed). The body
> returned when healthy is JSON, e.g.:
> `{"status":"healthy","db":"up","anthropic":"configured"}`
> BetterStack keyword check is case-sensitive by default — `"healthy"` in lowercase matches.

### Step 1.3 — Configure deploy maintenance windows

Deploys cause a brief downtime (container restart). Without a maintenance window, BetterStack
pages you during every deploy.

1. Go to **BetterStack → Uptime → Monitors → [select a monitor] → Maintenance windows**.
2. Create a recurring or one-off maintenance window that covers your deploy slot.
   Example: recurring Saturday 02:00-03:00 Europe/Berlin if you deploy off-hours.
3. Repeat for both monitors.

> For ad-hoc deploys outside the recurring window: use **BetterStack → Uptime → Maintenance
> windows → New maintenance window** to create a one-off window before each deploy.

### Step 1.4 — Create a status page

1. Go to **BetterStack → Status pages → New status page**.
2. Add both monitors (`/health` and `/api/v1/health`) to the page.
3. Set page title: e.g. `TaxReader Status`.
4. Note the public URL (e.g. `https://taxreader.betteruptime.com`).

### Step 1.5 — Link the status page from the site footer

Add a "Systemstatus" link to `Frontend/src/components/layout/footer.tsx`.

Current footer has these links: Impressum, Datenschutzerklärung, AGB, Widerrufsbelehrung, Cookie-Einstellungen.

Add after the existing links:

```tsx
<a
  href="https://<your-betterstack-status-page-url>"
  className="hover:text-foreground hover:underline"
  target="_blank"
  rel="noopener noreferrer"
>
  Systemstatus
</a>
```

Replace `<your-betterstack-status-page-url>` with the actual URL from Step 1.4.

> Keep the link as a plain `<a>` (external URL), not a Next.js `<Link>` (which is for
> internal routes).

---

## 2. Sentry — Quiet-Hours Alert Rule

Sentry is already integrated (wired in Phase 1, consent-gated in Phase 6). This step only
tunes the alert rules for solo-dev paging — no new installation required.

### Step 2.1 — Set the quiet-hours rule

1. Go to **Sentry → Alerts → Alert rules**.
2. Find the existing high-severity alert rule (or create a new one if none exists).
3. Edit / create:
   - **Name:** `TaxReader — High severity, no pages at night`
   - **Conditions:** issue is unresolved AND severity = `HIGH` or `CRITICAL`
   - **Filters / time window:**
     - Enable **"Do not alert during"** (or "Mute during") feature
     - Set window: `23:00 – 07:00` timezone `Europe/Berlin`
   - **Actions / notifications:**
     - Email: your operator email address
     - Push notification: Sentry mobile app or equivalent
4. Save.

> Effect: HIGH/CRITICAL issues still record to Sentry around the clock. The paging action
> (email + push) is suppressed 23:00-07:00. LOW/MEDIUM issues never page — they appear in
> the Sentry dashboard on your next working session.

### Step 2.2 — Verify the rule

In Sentry → Alerts → Alert rules, confirm:
- The rule appears with status **Active**.
- The notification channels show email + push.
- The mute window is set to 23:00-07:00 Europe/Berlin.

> If Sentry's "quiet hours" UI differs from the above (Sentry UI changes frequently), use
> the **"Ignore" schedule** or **"Mute" time-based filter** — the goal is the same: no pages
> between 23:00 and 07:00 unless HIGH/CRITICAL.

---

## 3. Verification Checklist

After completing the above steps, tick each item:

- [ ] BetterStack keyword monitor for `/health` shows **Up**
- [ ] BetterStack keyword monitor for `/api/v1/health` shows **Up**
- [ ] Both monitors use **keyword** check on `"healthy"` (not status-code only)
- [ ] Maintenance windows configured for both monitors
- [ ] Status page created and both monitors added to it
- [ ] Footer "Systemstatus" link added and resolves to the BetterStack status page
- [ ] Sentry quiet-hours rule active (23:00-07:00 Europe/Berlin, HIGH only, email + push)

---

## 4. Environment Variables (if provisioning via API later)

If you later want to automate monitor creation via the BetterStack REST API:

| Variable | Where to get it | Used for |
|----------|----------------|---------|
| `BETTERSTACK_API_TOKEN` | BetterStack → Account → API tokens | Creating/updating monitors via REST API |

Dashboard setup (Steps 1.1–1.5 above) requires no env var.

---

_Authored: Phase 7 Plan 07 (07-07)_
_Requirements: OBS-03 / QA-06 / D-08_
_Last updated: 2026-06-07_
