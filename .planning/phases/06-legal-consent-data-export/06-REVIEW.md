---
phase: 06-legal-consent-data-export
reviewed: 2026-06-03T05:39:50Z
depth: standard
files_reviewed: 48
files_reviewed_list:
  - Backend/src/TaxReader.Api/Endpoints/ExportEndpoints.cs
  - Backend/src/TaxReader.Api/Hangfire/RecurringJobsBootstrap.cs
  - Backend/src/TaxReader.Api/Program.cs
  - Backend/src/TaxReader.Application/Commands/DeleteAccountHandler.cs
  - Backend/src/TaxReader.Application/Commands/SaveClassificationRuleHandler.cs
  - Backend/src/TaxReader.Application/Interfaces/IAppDbContext.cs
  - Backend/src/TaxReader.Application/Interfaces/IAuditLogger.cs
  - Backend/src/TaxReader.Application/Interfaces/IExportTokenStore.cs
  - Backend/src/TaxReader.Application/Jobs/ExportCleanupJob.cs
  - Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs
  - Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs
  - Backend/src/TaxReader.Application/Jobs/RevokeTokensJob.cs
  - Backend/src/TaxReader.Domain/Entities/AuditLogEntry.cs
  - Backend/src/TaxReader.Domain/Enums/AuditAction.cs
  - Backend/src/TaxReader.Infrastructure/Data/AppDbContext.cs
  - Backend/src/TaxReader.Infrastructure/Data/Configurations/AuditLogEntryConfiguration.cs
  - Backend/src/TaxReader.Infrastructure/DependencyInjection.cs
  - Backend/src/TaxReader.Infrastructure/Migrations/20260603045456_AddAuditLog.cs
  - Backend/src/TaxReader.Infrastructure/Services/AuditLogger.cs
  - Backend/src/TaxReader.Infrastructure/Services/ExportTokenStore.cs
  - Backend/src/TaxReader.Infrastructure/Services/RefreshTokenService.cs
  - Backend/tests/TaxReader.UnitTests/Application/AuditAppendOnlyTests.cs
  - Backend/tests/TaxReader.UnitTests/Application/AuditLoggerTests.cs
  - Backend/tests/TaxReader.UnitTests/Application/Commands/SaveClassificationRuleHandlerTests.cs
  - Backend/tests/TaxReader.UnitTests/Application/ExportDownloadEndpointTests.cs
  - Backend/tests/TaxReader.UnitTests/Application/ExportTokenStoreTests.cs
  - Backend/tests/TaxReader.UnitTests/Auth/DeleteAccountHandlerTests.cs
  - Backend/tests/TaxReader.UnitTests/Jobs/ExportCleanupJobTests.cs
  - Backend/tests/TaxReader.UnitTests/Jobs/ExportUserDataJobTests.cs
  - Backend/tests/TaxReader.UnitTests/Jobs/GrantTokensJobTests.cs
  - Backend/tests/TaxReader.UnitTests/Jobs/RevokeTokensJobTests.cs
  - Frontend/instrumentation-client.ts
  - Frontend/src/app/(authenticated)/layout.tsx
  - Frontend/src/app/(authenticated)/settings/page.tsx
  - Frontend/src/app/(legal)/agb/page.tsx
  - Frontend/src/app/(legal)/datenschutz/page.tsx
  - Frontend/src/app/(legal)/impressum/page.tsx
  - Frontend/src/app/(legal)/layout.tsx
  - Frontend/src/app/(legal)/widerruf/page.tsx
  - Frontend/src/app/layout.tsx
  - Frontend/src/components/consent/consent-settings-dialog.tsx
  - Frontend/src/components/consent/cookie-banner.tsx
  - Frontend/src/components/layout/cookie-settings-link.tsx
  - Frontend/src/components/layout/footer.tsx
  - Frontend/src/hooks/use-data-export.ts
  - Frontend/src/lib/api-client.ts
  - Frontend/src/providers/auth-provider.tsx
  - Frontend/src/providers/consent-provider.tsx
findings:
  critical: 4
  warning: 7
  info: 3
  total: 14
status: issues_found
---

# Phase 06: Code Review Report

**Reviewed:** 2026-06-03T05:39:50Z
**Depth:** standard
**Files Reviewed:** 48
**Status:** issues_found

## Summary

This phase ships the DSGVO Art. 20 data-export pipeline (request → Hangfire job → zip → one-time download), a GDPR-compliant consent/cookie-banner system, legal pages (Impressum, Datenschutz, AGB, Widerruf), an audit log, and token grant/revoke Hangfire jobs. The bulk of the logic is sound — the IDOR ownership check on download is present, path traversal is not possible (token is hex-only GUID), and audit entries are correctly flagged as append-only.

Four blockers were found. The most severe is a **file stream leak** in the download endpoint that leaves zip files open if the response pipeline throws after `FileStream` is opened but before `Results.File` consumes it. A second blocker is a **consent race condition** in `ConsentProvider` that calls `Sentry.init()` during React's `useCallback` render phase for returning users without re-reading the stored consent on mount, meaning Sentry can be activated before the stored preference is loaded. The third blocker is a **double-init gap** in `instrumentation-client.ts` that runs at boot time for every page load but uses the `localStorage` value as it existed at boot, so a user who revokes consent mid-session is not stopped from Sentry being already initialized. The fourth is a **placeholder email address** (`[kontakt@taxreader.de]`) shipped in all four legal pages — this is a live product path; a placeholder in Impressum, AGB, Datenschutz, and Widerruf exposes the operator to a TMG §5 violation.

---

## Critical Issues

### CR-01: FileStream leaked if Results.File pipeline throws after stream open

**File:** `Backend/src/TaxReader.Api/Endpoints/ExportEndpoints.cs:115-127`

**Issue:** The `FileStream` is opened at line 115 but is never wrapped in a `using` or `try/finally`. `tokenStore.Invalidate(token)` is called at line 118 (before the response is sent), and `auditLogger.RecordAsync` at line 120 can throw (e.g., DB timeout). If either call throws after the `FileStream` is opened the handle is never disposed, leaving the file locked. Additionally, calling `Invalidate` before the response is actually delivered means a network failure during streaming silently consumes the one-time token without the user receiving the file — they cannot retry.

**Fix:** Open the stream inside `Results.File`'s callback contract, or wrap in a `try/finally`. Defer `Invalidate` to after the response is confirmed sent (or accept that it is a best-effort one-time guard and move audit logging before opening the stream):

```csharp
// Preferred: audit first, then open and stream. Results.File disposes the stream on its own.
await auditLogger.RecordAsync(
    AuditAction.DataExportDownloaded,
    actorUserId: currentUser.UserId,
    subjectUserId: currentUser.UserId,
    metadata: new Dictionary<string, object?> { ["token_prefix"] = token[..8] },
    cancellationToken);

// Invalidate before opening stream — if open fails, user gets 500 and can retry with same token.
tokenStore.Invalidate(token);

var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read,
    FileShare.Read, bufferSize: 4096, useAsync: true);
return Results.File(stream, "application/zip", "taxreader-export.zip");
```

If auditing must come after streaming, use `IHttpContextAccessor` to hook `Response.OnCompleted` for the invalidation and audit call, and open the stream in a `try/finally` that disposes it on exception paths.

---

### CR-02: Sentry consent check race — Sentry may activate before stored preference is loaded

**File:** `Frontend/src/providers/consent-provider.tsx:47-51` and `Frontend/src/providers/consent-provider.tsx:87-91`

**Issue:** `ConsentProvider` initialises `consent` state from `DEFAULT_CONSENT` (line 66) — `fehleranalyse: false`, `decided: false` — and then reads `localStorage` in a `useEffect` (line 70). The `acceptAll` callback at line 87 calls `grantSentry()` synchronously. However, `grantSentry()` checks `!Sentry.isInitialized()` — not the React state — so it is possible for the following sequence to occur on a **returning user**:

1. `instrumentation-client.ts` runs at boot; if `localStorage` has `fehleranalyse: true`, `Sentry.init` is called there. ✓ Correct.
2. On the next visit after the user *revokes* consent (sets `fehleranalyse: false`), `instrumentation-client.ts` correctly skips init. ✓ Correct.
3. **But:** If `acceptAll` is somehow invoked before the `useEffect` fires (impossible in React but scroll down to the real gap) — this is not the actual race.

The actual gap: `grantSentry()` calls `Sentry.init()` only when `!Sentry.isInitialized()`. After `Sentry.close()` is called by `revokeSentry()`, `Sentry.isInitialized()` returns `false` again (Sentry resets its internal flag after close). The `void Sentry.close(2000)` at line 57 is fire-and-forget: if the user rapidly toggles consent on → off → on, `close()` may not have completed before the second `grantSentry()` call, so `!Sentry.isInitialized()` may return `true` and Sentry is re-inited while the first close is still running. This creates a window where two Sentry SDK instances overlap.

**Fix:** Track consent grant/revoke with a local `ref` flag rather than relying on `Sentry.isInitialized()` to gate re-initialization:

```typescript
const sentryGranted = useRef(false);

function grantSentry() {
  if (process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true" && !sentryGranted.current) {
    sentryGranted.current = true;
    if (!Sentry.isInitialized()) Sentry.init(sentryConfig());
  }
}

async function revokeSentry() {
  if (sentryGranted.current) {
    sentryGranted.current = false;
    await Sentry.close(2000);
  }
}
```

This ensures a rapid toggle cannot produce a double-init.

---

### CR-03: instrumentation-client.ts always exports `onRouterTransitionStart` even when Sentry is not initialized

**File:** `Frontend/instrumentation-client.ts:38`

**Issue:** `export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;` executes unconditionally regardless of whether `Sentry.init()` was called. When `NEXT_PUBLIC_SENTRY_ENABLED !== "true"` or the user has not given consent, `Sentry.init()` is never called, but `Sentry.captureRouterTransitionStart` is a live Sentry SDK function that **queues a breadcrumb event in Sentry's envelope transport**. If the SDK is not initialized, current Sentry SDK versions no-op safely, but this is an undocumented guarantee and differs across SDK major versions. More concretely, if a future SDK update causes this to throw or to call network transport before initialization, it will silently break navigation tracking for all users regardless of consent.

**Fix:** Guard the export behind the same initialization check:

```typescript
export const onRouterTransitionStart =
  process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true" && hasSentryConsent()
    ? Sentry.captureRouterTransitionStart
    : undefined;
```

Note: Next.js 16 instrumentation-client allows `undefined` exports for optional hooks.

---

### CR-04: Placeholder contact details in all legal pages shipped to production

**File:** `Frontend/src/app/(legal)/impressum/page.tsx:25-40`, `Frontend/src/app/(legal)/datenschutz/page.tsx:29-43`, `Frontend/src/app/(legal)/agb/page.tsx:83-90`, `Frontend/src/app/(legal)/widerruf/page.tsx:32-38`

**Issue:** All four legal pages contain literal placeholder text `[Name]`, `[Anschrift]`, `[PLZ Ort]`, and `[kontakt@taxreader.de]` (with brackets) in displayed copy. The `href="mailto:[kontakt@taxreader.de]"` links are syntactically broken (the brackets are not valid in an RFC 2822 address and will not open a mail client). Under §5 TMG an Impressum with missing or fictitious contact details is a legal violation with fines. Under DSGVO Art. 13 a privacy policy without a reachable controller address is also non-compliant.

This is classified BLOCKER rather than info because the DSGVO/TMG compliance is a stated hard requirement for the commercial DE launch and these pages are already routed and publicly accessible in the application.

**Fix:** Replace all `[…]` tokens with real operator data before any public deployment. Add a CI check (e.g. `grep -r '\[kontakt@' Frontend/src/app/\(legal\)/`) that fails the build if placeholder brackets remain.

---

## Warnings

### WR-01: AuditLogger calls SaveChangesAsync in isolation — bypasses outer transaction

**File:** `Backend/src/TaxReader.Infrastructure/Services/AuditLogger.cs:25`

**Issue:** `AuditLogger.RecordAsync` calls `dbContext.SaveChangesAsync()` immediately after adding the `AuditLogEntry`. The `AuditLogger` is injected into handlers that also own a unit-of-work and call `SaveChangesAsync` themselves (e.g. `SaveClassificationRuleHandler` saves the rule at line 61, then calls `auditLogger.RecordAsync` at line 64 which does a *second* `SaveChangesAsync`). If the handler's own save succeeds but the audit save fails, the operation is partially committed — the business entity exists but no audit trail. Conversely, if the audit save succeeds but a subsequent operation (not present in the reviewed handlers, but possible in future handlers) fails and rolls back, the audit row is already committed and cannot be rolled back.

The audit intent is append-only and post-commit, but the implementation makes it part of two sequential independent commits, not one atomic unit.

**Fix:** Either (a) let the caller include the audit entry in its own `SaveChangesAsync` call by having `RecordAsync` only `Add` without saving (rename to `Append`; require callers to commit), or (b) use `IDbContextTransaction` to wrap both saves. Option (a) is simpler and aligns with the existing pattern. Update `IAuditLogger` accordingly:

```csharp
// IAuditLogger — append only, caller commits
void Append(AuditAction action, Guid? actorUserId, Guid? subjectUserId,
    Dictionary<string, object?> metadata);
```

---

### WR-02: ExportUserDataJob has no error handler — a failed job marks token Generating forever

**File:** `Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs:28-173`

**Issue:** `ExportUserDataJob` is decorated with `[AutomaticRetry(Attempts = 3)]`. If all three retries are exhausted (e.g. disk full, DB error), the job moves to Hangfire's Failed state, but `tokenStore.MarkGenerating` was already called by the request endpoint (line 30 of `ExportEndpoints.cs`). The token remains in `Generating` state in `ExportTokenStore` indefinitely — it will never transition to `Expired` because the expiry-flip logic in `TryGet` only fires for `Status != Generating` (line 37 of `ExportTokenStore.cs`). The user sees a permanent spinner with no way to trigger a new export without a session reset.

**Fix:** Wrap the entire `HandleAsync` body in a try/catch and call `tokenStore.Invalidate(exportToken)` in the catch block so the status endpoint returns "not found → Expired" and the user can retry:

```csharp
catch (Exception ex)
{
    logger.LogError(ex, "Data export failed for User {UserId}", userId);
    tokenStore.Invalidate(exportToken);
    throw; // re-throw so Hangfire records the failure
}
```

Alternatively, add an `ExportFailed` status to `IExportTokenStore` so the UI can show a concrete error instead of the generic "Expired" fallback.

---

### WR-03: ExportCleanupJob test writes to the shared global temp directory

**File:** `Backend/tests/TaxReader.UnitTests/Jobs/ExportCleanupJobTests.cs:34-63`

**Issue:** `HandleAsync_OldZipFiles_DeletesThemAndKeepsFreshOnes` creates files in `Path.Combine(Path.GetTempPath(), "taxreader-exports")` — the same directory the real production job targets. If a parallel test run or a running dev server has a live export in that directory at the time the test runs, the test will delete it (since `File.SetCreationTimeUtc` sets the old file's time to `DateTime.UtcNow.AddHours(-25)`, but any *other* file already older than 24 hours is also deleted). The constructor creates `_testExportsDir` as an isolated directory but never uses it — the actual test uses `realExportsDir`. `Dispose` cleans up `_testExportsDir` (which is empty) but not `realExportsDir`.

This is a test reliability issue that can also cause data loss in development environments running the full stack while the tests execute.

**Fix:** Either (a) make `ExportCleanupJob` accept a configurable directory via constructor/options (preferred — improves testability and removes the hardcoded path), or (b) use the isolated `_testExportsDir` and a subclass that overrides the directory. Remove `_testExportsDir` from the constructor if it goes unused.

---

### WR-04: ExportTokenStore does not expire Generating tokens — user is permanently stuck after job failure

**File:** `Backend/src/TaxReader.Infrastructure/Services/ExportTokenStore.cs:37`

**Issue:** The expiry-flip logic in `TryGet` is:

```csharp
if (stored.Status != ExportStatus.Generating && DateTime.UtcNow > stored.ExpiresAtUtc)
```

A token in `Generating` state will **never** be flipped to `Expired`, even if `ExpiresAtUtc` has passed. The status endpoint returns `"Generating"` forever. This compounds WR-02: even without a job failure, if the container restarts after `MarkGenerating` but before `Register`, the token is stuck in `Generating` (note: container restart also wipes the in-memory store, so the user gets "not found" which maps to "Expired" — that case is actually handled correctly). The problematic case is a long-running DB query that exceeds the token's TTL during the Hangfire retry window.

**Fix:** Remove the `Generating` exception from the expiry check:

```csharp
if (DateTime.UtcNow > stored.ExpiresAtUtc)
{
    record = stored with { Status = ExportStatus.Expired };
    return true;
}
```

This ensures a stuck Generating token eventually becomes Expired and the user can re-trigger the export.

---

### WR-05: GrantTokensJob payment match is ambiguous and can grant credits to the wrong payment record

**File:** `Backend/src/TaxReader.Application/Jobs/GrantTokensJob.cs:52-58`

**Issue:** The job finds the payment to update by matching `UserId`, `Status == Pending`, and `CreditsGranted == credits` — then takes the *most recent* matching row. If a user has purchased the same credit pack twice in quick succession (two Pending payments with identical `CreditsGranted`), the first Hangfire job (for the first Stripe event) correctly updates the most recent payment. The second Hangfire job (for the second Stripe event) then finds the *same* (now Granted) record's peer — but only if there is still a Pending row for that amount. If two identical Stripe events fire and both jobs execute nearly simultaneously, both could match and mark the same payment row as Granted, or one could fail to find a Pending row at all and leave one purchase without a granted payment record. The Stripe `StripeEventId` on the `Payment` entity is not used for this lookup.

**Fix:** Pass the Stripe event ID through `GrantTokensJob.HandleAsync` and match by `StripeEventId` instead of by credit amount:

```csharp
var payment = await dbContext.Payments
    .Where(p => p.StripeEventId == stripeEventId && p.Status == PaymentStatus.Pending)
    .FirstOrDefaultAsync(cancellationToken);
```

This is idempotent (Stripe guarantees event IDs are unique) and eliminates the ambiguous match.

---

### WR-06: ConsentProvider reads consent from localStorage on mount but Sentry may already be initialized by instrumentation-client.ts — revoke path is not triggered on first render

**File:** `Frontend/src/providers/consent-provider.tsx:70-85`

**Issue:** `instrumentation-client.ts` calls `Sentry.init()` at module load time if the stored consent has `fehleranalyse: true`. `ConsentProvider` then mounts and reads the same value from `localStorage` in `useEffect`. If the stored value is `fehleranalyse: true`, `ConsentProvider` sets state accordingly and does nothing (no call to `grantSentry()` since Sentry is already initialized). This is correct.

However, the `useEffect` fires *after* the first render. During SSR (Next.js server render), `localStorage` is not available; `consent` starts as `DEFAULT_CONSENT` (`fehleranalyse: false`). On the client, hydration completes and then the `useEffect` fires. Between SSR completion and `useEffect` execution, components that read `consent.fehleranalyse` (e.g. the cookie banner's `consent.decided` check) render with the default values. The cookie banner will flash visible for one render cycle for returning users who previously set `decided: true`, causing a visible layout shift.

**Fix:** Store the consent key in a cookie (readable server-side by Next.js middleware/layout) in addition to `localStorage`. Server components can then suppress the banner or pass the initial state as a prop, eliminating the flash. Alternatively, use `suppressHydrationWarning` on the banner's container, which is already applied to `<html>` (line 34 of `layout.tsx`) but not propagated to children.

---

### WR-07: deleteAccount in api-client.ts does not clear the in-memory accessToken on auth failure paths

**File:** `Frontend/src/lib/api-client.ts:132-142`

**Issue:** `deleteAccount` calls `clearAuthStorage()` at line 141 only on the *success* path (after the `await axios.delete(...)` resolves without throwing). If the request throws (wrong password → 401, network error, etc.), `clearAuthStorage()` is never called. This is intentional for the wrong-password case. However, if the backend returns 200 with a body that causes axios to throw (extremely unlikely but possible with a misconfigured proxy), or if a 5xx triggers the interceptor's logout path, `accessToken` in module scope is cleared by the interceptor (`clearAuthStorage` is called at line 71) but `deleteAccount`'s caller in `settings/page.tsx` will catch `err` from the axios throw, not from `clearAuthStorage`. The caller then sets `setIsDeleting(false)` (line 114) and keeps the user logged in — correct behavior. But because the `_retry` flag on the `originalRequest` prevents the 401-interceptor from re-firing, and because `deleteAccount` uses a raw `axios` instance (not the shared `api` instance), the interceptor is **not involved at all** for `deleteAccount`. The existing code is therefore correct for the delete flow.

However, `clearAuthStorage()` at line 141 is called *before* the function returns (i.e., on the happy path where the delete succeeded). The caller `handleDeleteAccount` in `settings/page.tsx` then calls `logout()` from `useAuth` (line 104), which calls `apiLogout()` → `clearAuthStorage()` again (line 130). This is a double-clear that is harmless but redundant. More importantly, `logout()` also calls `setUser(null)` and `router.replace('/login')`, which is the correct UX. Since `deleteAccount` already calls `clearAuthStorage()` at line 141, if `handleDeleteAccount` were ever refactored to skip `logout()`, the user object would persist in React state while localStorage is cleared — a state inconsistency.

**Fix:** Remove the `clearAuthStorage()` call from inside `deleteAccount` (line 141) and rely exclusively on the caller's `logout()` invocation to clear state. This makes the function's contract consistent: it only performs the HTTP delete; cleanup is the caller's responsibility.

```typescript
export async function deleteAccount(password: string): Promise<void> {
  await axios.delete("/api/v1/auth/account", {
    headers: { Authorization: `Bearer ${getAccessToken()}` },
    data: { password },
  });
  // Caller is responsible for calling logout() to clear all auth state.
}
```

---

## Info

### IN-01: ExportUserDataJob — parsedReceipts variable unused in CSV output (field naming inconsistency)

**File:** `Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs:50-62` and `133-138`

**Issue:** The variable `receipts` at line 37 maps `ReceiptFile` rows, while `parsedReceipts` at line 50 maps `Receipt` rows. The zip uses `receipts.json` / `receipts.csv` for `ReceiptFile` data, but `parsedReceipts` is written to `items.json` — wait, actually `parsedReceipts` is not written to any zip entry at all. Lines 133-138 write `receipts.json` from `receipts` (the `ReceiptFile` projection), and `items.json` / `items.csv` from `items` (the `ReceiptItem` projection). The `parsedReceipts` collection (parsed `Receipt` rows with vendor/purchase_date/total_amount) is **silently excluded from the export bundle**. Users are entitled to their parsed receipt data under DSGVO Art. 20.

**Fix:** Add a zip entry for `parsedReceipts`:

```csharp
await WriteJsonEntryAsync(archive, "parsed_receipts.json", parsedReceipts, jsonOptions, cancellationToken);
await WriteCsvEntryAsync(archive, "parsed_receipts.csv",
    "id,receipt_file_id,vendor,purchase_date,total_amount,currency,parsed_at",
    parsedReceipts.Select(r =>
        $"{r.id},{r.receipt_file_id},{EscapeCsv(r.vendor)},{r.purchase_date},{r.total_amount},{r.currency},{r.parsed_at:o}"),
    cancellationToken);
```

Also update `README.txt` to include the new entry description.

---

### IN-02: Legal pages ship with `DraftWarning` component visible to end users

**File:** `Frontend/src/app/(legal)/agb/page.tsx:7-13`, `Frontend/src/app/(legal)/datenschutz/page.tsx:7-13`, `Frontend/src/app/(legal)/impressum/page.tsx:7-13`, `Frontend/src/app/(legal)/widerruf/page.tsx:7-13`

**Issue:** Every legal page renders a yellow warning banner: "Entwurf – anwaltliche Prüfung ausstehend". This is appropriate for internal review, but it is rendered in the same component as production page content with no environment gate. Any end user visiting `/agb`, `/datenschutz`, `/impressum`, or `/widerruf` will see this warning. For a commercial launch this is legally misleading (implies the document is not final while it is presented as the binding contract).

**Fix:** Remove the `DraftWarning` component before launch, or gate it on `process.env.NODE_ENV === "development"` / an internal-only flag:

```tsx
{process.env.NODE_ENV !== "production" && <DraftWarning />}
```

---

### IN-03: AuditAppendOnlyTests path computation is fragile and will silently pass on wrong path

**File:** `Backend/tests/TaxReader.UnitTests/Application/AuditAppendOnlyTests.cs:14-21`

**Issue:** `SourceRoots` is computed via `Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "...", "Backend", "src", "TaxReader.Application"))`. If the test output directory structure changes (e.g. build artifacts move, CI uses a non-standard publish path), `Directory.Exists(dir)` at line 27 returns `false`, and `AllSourceLines` returns an empty enumerable. The test `AuditLogEntries_HasNoRemoveOrDeleteCallsInApplicationOrInfrastructure` will then pass vacuously — zero violations found because zero lines were scanned.

The test has no assertion that the source directories were actually found and non-empty, so a wrong-path failure is indistinguishable from a passing scan.

**Fix:** Add an assertion that the source directory exists and contains at least one file before running the forbidden-pattern scan:

```csharp
foreach (var root in SourceRoots)
{
    Directory.Exists(root).Should().BeTrue(
        $"source root '{root}' must exist for the append-only scan to be meaningful");
}
```

---

_Reviewed: 2026-06-03T05:39:50Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
