---
phase: 06-legal-consent-data-export
reviewed: 2026-06-05T00:00:00Z
depth: standard
files_reviewed: 7
files_reviewed_list:
  - Backend/src/TaxReader.Api/Endpoints/ExportEndpoints.cs
  - Backend/src/TaxReader.Application/Interfaces/IExportTokenStore.cs
  - Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs
  - Backend/src/TaxReader.Infrastructure/Services/ExportTokenStore.cs
  - Backend/tests/TaxReader.UnitTests/Application/ExportTokenStoreTests.cs
  - Backend/tests/TaxReader.UnitTests/Jobs/ExportUserDataJobTests.cs
  - .github/workflows/ci.yml
findings:
  critical: 0
  warning: 2
  info: 4
  total: 6
status: issues_found
---

# Phase 6: Code Review Report (Gap-Closure Re-Review)

**Reviewed:** 2026-06-05
**Depth:** standard
**Files Reviewed:** 7
**Status:** issues_found

## Summary

Re-review of the Phase 6 gap-closure diff (base `cb6f1ea`) covering the LEG-07 export fixes (parsed_receipts bundle entries, download resource-safety, job-failure + Generating-past-TTL recovery to terminal `Expired`) and CR-04 (the CI legal-placeholder guard).

The gap-closure work is sound. The documented design decisions hold up under scrutiny: the audit-before-stream / invalidate-after-stream ordering in the download handler correctly prevents both a leaked `FileStream` handle and premature one-time-token consumption; `Results.File` owning disposal is the right idiom; reusing `Expired` as the recoverable terminal state is reasonable and avoids frontend churn; `MarkExpired` idempotency and the `TryGet` TTL flip for `Generating` tokens are correct and well-tested. Logging uses named placeholders, `CancellationToken` is threaded, no exceptions are used for control flow, and German user-facing copy is present. The IDOR ownership check is type-safe (`Guid` vs `Guid`).

No blockers found in the reviewed diff. Two warnings concern CSV-export robustness/security on user-controlled fields, and the CI guard's regex breadth. The info items document accepted trade-offs and minor robustness notes for follow-up.

## Warnings

### WR-01: CSV export does not guard against formula (CSV) injection on user-controlled fields

**File:** `Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs:142-145, 244-250`
**Issue:** The new `parsed_receipts.csv` writes the user-influenced `vendor` field (parsed from receipt content) into a CSV cell using the shared `EscapeCsv` helper. `EscapeCsv` only quotes values containing `,`, `"`, or `\n` — it does not neutralize cells beginning with `=`, `+`, `-`, or `@`. When the exported CSV is opened in Excel / LibreOffice / Google Sheets, a cell like `=HYPERLINK(...)` or `=cmd|'/c calc'!A1` is interpreted as a formula and can execute. Because this is a DSGVO Art. 20 self-serve export of receipt data whose `vendor` (and, via the same helper, `description`, `original_file_name`, `source_hint`, `reason`) originate from uploaded/parsed PDFs, the values are attacker-influenceable. The newly added line in this diff (`vendor`) extends the same exposure to a parsed text field.

This is a pre-existing pattern, but the diff adds a new user-controlled field to a CSV using the unguarded helper, so it is in scope.

**Fix:** Prefix at-risk cells with a single quote (or a tab) when the value starts with a formula trigger, inside `EscapeCsv`:
```csharp
private static string EscapeCsv(string? value)
{
    if (string.IsNullOrEmpty(value)) return string.Empty;

    // CSV formula-injection guard: neutralize cells a spreadsheet would evaluate.
    if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        value = "'" + value;

    if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        return $"\"{value.Replace("\"", "\"\"")}\"";

    return value;
}
```

### WR-02: EscapeCsv does not quote values containing a bare carriage return

**File:** `Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs:247`
**Issue:** The quoting condition checks `'\n'` but not `'\r'`. A field value containing a bare `\r` (CR without LF) is written unquoted. `StreamWriter.WriteLineAsync` terminates rows with the environment newline; an embedded `\r` inside an unquoted cell can be interpreted by strict CSV readers (RFC 4180) as a record boundary, corrupting row alignment for downstream consumers of the export. Parsed receipt text (vendor, description, reason) can contain `\r` from OCR/extraction.
**Fix:** Add `\r` to the quoting predicate (folded into the WR-01 fix above): `value.Contains('\r')` triggers quoting, so any CR is preserved inside a quoted cell rather than splitting the row.

## Info

### IN-01: Audit event can be recorded more than once for a single successful one-time download (TOCTOU window)

**File:** `Backend/src/TaxReader.Api/Endpoints/ExportEndpoints.cs:104-129`
**Issue:** The handler checks `File.Exists(zipPath)` (line 104), then records the `DataExportDownloaded` audit event (line 116), then opens the `FileStream` (line 125), then invalidates the token (line 129). The audit-before-stream ordering is the intended CR-01 fix and is correct. However, if the file is deleted between the `File.Exists` check and the `new FileStream(...)` open (container cleanup, TTL sweep, race), the `FileStream` constructor throws after the audit row is already written and before `Invalidate` runs. The token survives, so the client can retry — recording a second `DataExportDownloaded` audit entry. The net effect is a duplicate/false audit row and an unhandled exception surfacing via `ExceptionHandlingMiddleware` (HTTP 500). This is a narrow window and consistent with the documented "audit before delivery" decision; noting it so the duplicate-audit semantics are a conscious choice rather than an oversight.
**Fix:** Optionally wrap the `new FileStream(...)` open in a try/catch that, on `IOException`/`FileNotFoundException`, calls `tokenStore.Invalidate(token)` and returns the same 410 "nicht mehr verfügbar" problem as the missing-file branch. Acceptable to defer.

### IN-02: Job catch block treats cancellation as a failure (logs error + flips token to Expired)

**File:** `Backend/src/TaxReader.Application/Jobs/ExportUserDataJob.cs:182-192`
**Issue:** `catch (Exception ex)` also catches `OperationCanceledException` raised by `cancellationToken.ThrowIfCancellationRequested()` or cancelled EF/IO calls. On cancellation the job logs `LogError` ("Data export failed...") and calls `MarkExpired`, then rethrows. For a normally-cancelled job this misclassifies a cooperative cancellation as an error in the logs/alerting. Behaviourally harmless (a retry re-flips to Ready), but it adds noise to error-based paging.
**Fix:** Optionally let cancellation propagate without the error-log/MarkExpired side effects:
```csharp
catch (OperationCanceledException) { throw; }
catch (Exception ex) { /* existing log + MarkExpired + throw */ }
```

### IN-03: CI legal-placeholder guard regex can false-positive on TSX bracket syntax

**File:** `.github/workflows/ci.yml:55`
**Issue:** The CR-04 guard greps `\[[^]]+\]` across all files under `Frontend/src/app/(legal)`. These are `.tsx` files; legitimate TypeScript/JSX uses square brackets (array types `string[]`, indexing `arr[0]`, array literals, `useState([])`, `className={[...]}`). If a future legal page introduces any such syntax, the guard will fail the build with a false positive, blocking merges to `main`. The current pages are static content so this is latent, not active.
**Fix:** Narrow the match to the known operator placeholders rather than any bracketed token, e.g. `\[(Name|Anschrift|PLZ Ort|kontakt@[^]]+)\]` (or restrict to JSX text nodes). Lower priority given the pages are static prose.

### IN-04: Legal pages still contain unfilled bracket placeholders — CI guard is currently red

**File:** `.github/workflows/ci.yml:47-60` (guard) vs `Frontend/src/app/(legal)/*` (out of reviewed scope)
**Issue:** The CI guard works exactly as designed and currently fires: `impressum`, `datenschutz`, `agb`, and `widerruf` pages still contain `[Name]`, `[Anschrift]`, `[PLZ Ort]`, and `[kontakt@taxreader.de]` placeholders (confirmed by running the guard regex). This is the intended forcing function (operator must replace them before deploy), not a defect in the reviewed diff. Recorded so it is visible that the guard will block all `push`/`pull_request` to `main` until the operator fills in real TMG §5 contact data. The legal page files are outside this review's file scope.
**Fix:** No code fix — operator action: replace the placeholder tokens with real contact data, then CI passes.

---

_Reviewed: 2026-06-05_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
