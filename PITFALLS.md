# Looks done but isn't — Pre-launch Verification Checklist

> Run through every item below before marking the milestone **GO**. Each checkbox
> represents something that looks complete but silently fails, leaks, or deceives without
> this explicit verification. Mark `[x]` only after the stated verification method passes.

---

## Section A — Test-Infrastructure Traps

Seven patterns that make tests appear to pass while hiding real bugs.

- [ ] **Pitfall 1 — Hangfire schema init in the test host fails silently.**
  Hangfire shares `DefaultConnection` with `AppDbContext`. If the WAF override is missing
  or Hangfire throws at init, the whole host never starts and every test is skipped rather
  than failing.
  _Verify:_ Run `dotnet test Backend/tests/TaxReader.IntegrationTests` with `--verbosity normal`
  and confirm Hangfire tables (`hangfire.*`) appear in the container DB after host startup.

- [ ] **Pitfall 2 — Parallel `WebApplicationFactory<Program>` instances crash on top-level
  `Program.cs` statements.**
  Tests pass in isolation, fail flakily when the full suite runs.
  _Verify:_ Confirm `[CollectionDefinition(DisableParallelization = true)]` is set on
  `IntegrationTestCollection` and all QA-01 test classes carry `[Collection("Postgres integration (shared container)")]`.

- [ ] **Pitfall 3 — Respawn deletes `__EFMigrationsHistory` and Hangfire tables.**
  Second test in a collection sees "relation does not exist" or an unmigrated schema.
  _Verify:_ Check `RespawnerOptions.TablesToIgnore` includes `"__EFMigrationsHistory"` and
  all Hangfire tables (or `TablesToIgnore` is combined with an excluded schema for Hangfire).
  Confirm migrations run ONCE at container init, before the Respawn checkpoint is created.

- [ ] **Pitfall 4 — DE-localization guard false positives on code identifiers.**
  A naive `grep` flags variable names, ARIA roles, `className` values, and import paths as
  English user-facing strings, making the guard noisy and ignored.
  _Verify:_ The guard's grep patterns are scoped to JSX text nodes and known string locations.
  The allow-list covers legitimate English tokens (brand names, technical symbols). The guard
  passes on a fully-German codebase without false failures.

- [ ] **Pitfall 5 — Playwright/Testcontainers fail because the CI runner lacks Docker or
  browser binaries.**
  "Cannot connect to the Docker daemon" or "Executable doesn't exist at .../chromium".
  _Verify:_ The heavy CI job uses `ubuntu-latest` (Docker present by default), has an
  explicit `npx playwright install --with-deps` step, and caches `~/.cache/ms-playwright`.
  The container image is pinned to `postgres:17-alpine` matching `docker-compose.yml`.

- [ ] **Pitfall 6 — BetterStack status-code-only monitor misses real degradation.**
  Monitor shows green while the DB is down but the endpoint still returns 200.
  _Verify:_ Both monitors (`/health` and `/api/v1/health`) are configured as **keyword monitors**
  asserting the response body contains `"healthy"` — NOT plain HTTP/status-code monitors.
  Confirmed in the BetterStack dashboard under Uptime → Monitors → each monitor's
  "Response keywords" setting.

- [ ] **Pitfall 7 — In-memory idempotency test gives false confidence.**
  `StripeWebhookHandlerTests` "passes" against the in-memory EF provider, which does NOT
  enforce the `stripe_event_id` UNIQUE index. Only the real Postgres constraint catches a
  concurrent duplicate insert.
  _Verify:_ QA-01 `PaymentIdempotencyTests` runs against Testcontainers Postgres and asserts
  the second insert with the same `stripe_event_id` throws `DbUpdateException` (Postgres 23505).

---

## Section B — Security Must-Not-Leak

These items ship silently broken if not verified explicitly.

- [ ] **Health endpoints contain no secrets.**
  `/health` and `/api/v1/health` must NOT include connection strings, Anthropic API keys,
  JWT secrets, stack traces, or internal hostnames.
  _Verify:_ `curl https://<domain>/health` and `curl https://<domain>/api/v1/health` — inspect
  the full JSON body. Confirm only status fields (`status`, `db`, `anthropic`) are present.

- [ ] **Both health endpoints are anonymous (not 401-gated).**
  BetterStack probes are unauthenticated. A `RequireAuthorization()` gate blocks every probe.
  _Verify:_ Call both endpoints without a JWT. Expect `200` with a JSON body, not `401`.

- [ ] **Refresh-token replay revokes all tokens (real DB, not in-memory).**
  The `token_hash` UNIQUE constraint in Postgres anchors replay detection; in-memory DB
  ignores it and gives false confidence.
  _Verify:_ QA-01 `RefreshTokenRotationReplayTests` asserts that replaying an already-rotated
  token returns a failure result AND that all of the user's active tokens are revoked.
  Test runs against Testcontainers Postgres (`token_hash` UNIQUE enforced).

- [ ] **Export one-time-token IDOR check holds (LEG-07).**
  A second account must receive `403` on another user's export token; a second download
  attempt on the same token must return `404` (expired).
  _Verify:_ 06-HUMAN-UAT.md item 7 is exercised: two-user IDOR test passes, and a second
  download of the same one-time token returns `404`.

---

## Section C — Localization

- [ ] **No bare `toLocaleString()` without `de-DE` locale.**
  Currency formatted without `'de-DE'` renders `$1,234.56` or `1.234,56 €` depending on the
  user's browser locale — unpredictable and wrong for a DE product.
  _Verify:_ DE-localization CI guard (QA-04) passes on `main`. Guard asserts no
  `toLocaleString()` call without an explicit `de-DE` locale; money goes only through
  `formatCurrency` / `Intl.NumberFormat('de-DE', { style: 'currency', currency: 'EUR' })`.

- [ ] **All money values display in German format (1.234,56 €).**
  _Verify:_ Spot-check the dashboard totals, the receipt detail page, and the PDF/CSV export
  in the browser — every monetary value shows German thousand-separator and comma decimal.

- [ ] **Native-speaker DE review completed** _(non-blocking per D-06 — track, do not gate)._
  _Verify:_ At least one German native speaker has reviewed all user-facing copy for `Sie`-form
  consistency, natural phrasing, and absence of Denglisch. Note reviewer name + date here.
  Result: `[ ] done — reviewer: ___________ date: ___________`

---

## Section D — Legal / Launch

- [ ] **Legal placeholders filled — 06-07 CI guard is green.**
  `[Name]`, `[Anschrift]`, `[PLZ Ort]`, `[kontakt@taxreader.de]`, and any other `[bracketed]`
  tokens must be replaced with real legal-entity data in all four legal pages before the
  `hygiene-check` CI job passes.
  _Verify:_ `hygiene-check` job in CI is green on `main`. Or locally:
  `grep -rn '\[' Frontend/src/app/\(legal\)/` returns no output.

- [ ] **Lawyer sign-off on AGB + Datenschutzerklärung — draft markers removed.**
  `<DraftWarning />` components on all four legal pages must be removed after lawyer review.
  _Verify:_ `06-LEGAL-REVIEW.md` shows **Lawyer-reviewed** for Impressum, Datenschutzerklärung,
  AGB, and Widerrufsbelehrung. `grep -rn 'DraftWarning' Frontend/src/app/\(legal\)/` returns
  no output.

- [ ] **All four AVVs/DPAs signed (Anthropic, Stripe, Sentry, BetterStack).**
  DSGVO Art. 28 requires signed Auftragsverarbeitungsverträge with every sub-processor
  before processing personal data commercially.
  _Verify:_ `06-AVV-TRACKING.md` "Signed" column shows `✓ YYYY-MM-DD` for all four rows and
  "Link in Datenschutz" column shows `✓` for all four.

- [ ] **StBerG-safe copy confirmed — no "tax advice" language.**
  Every user-facing string in the app and legal pages uses "Helfer, not Berater" framing.
  No sentence promises a tax outcome, guarantees a deduction, or constitutes Steuerberatung.
  _Verify:_ AGB §1 scope clause reviewed by lawyer; lawyer sign-off (see item above).

---

## Section E — Ops Readiness

- [ ] **BetterStack keyword monitors live and reporting "up".**
  Both `/health` (DB ping) and `/api/v1/health` (DB + Anthropic config) have active keyword
  monitors asserting `"healthy"`.
  _Verify:_ BetterStack dashboard → Uptime → Monitors shows both monitors as **Up**. Each
  monitor uses a keyword check (not status-code only) — see 07-OPS-SETUP.md.

- [ ] **BetterStack status page linked from site footer.**
  Users and operators need a public status page for incident communication.
  _Verify:_ Footer in production shows a "Systemstatus" link. Clicking it opens the
  BetterStack-hosted status page. See 07-OPS-SETUP.md and `Footer` component
  (`Frontend/src/components/layout/footer.tsx`).

- [ ] **Sentry quiet-hours alert rule set.**
  23:00–07:00 Europe/Berlin: only HIGH-severity pages go out (email + push).
  _Verify:_ Sentry → Alerts → Alert rules shows a rule scoped to 23:00-07:00 Europe/Berlin,
  HIGH severity only, channel email + push. See 07-OPS-SETUP.md.

- [ ] **Deploy maintenance windows configured in BetterStack.**
  Deploys during the launch period must not trigger false-positive pages.
  _Verify:_ BetterStack → Monitors → each monitor has a maintenance window configured to
  cover planned deploy windows. See 07-OPS-SETUP.md.

---

## How to use this checklist

1. Work through each section top to bottom before go/no-go.
2. Items without `[ ]` cannot be marked done by assumption — run the stated verification.
3. Section D items (legal) and Section E items (ops) that remain unchecked are **D-05 hard
   blockers** — do not ship until they are checked.
4. Section C native-speaker review is **non-blocking per D-06** — track it, do not gate launch.
5. After go/no-go, update `07-GO-NO-GO.md` with evidence links for each D-05 item.

---

_Authored: Phase 7 Plan 07 (07-07)_
_Requirement: QA-07_
_Last updated: 2026-06-07_
