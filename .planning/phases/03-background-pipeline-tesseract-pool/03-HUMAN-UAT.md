---
status: pending
phase: 03-background-pipeline-tesseract-pool
plan: 03-04
requirements: [PIPE-05, PIPE-06]
created: 2026-05-22T00:00:00Z
---

# Plan 03-04 Manual UAT Checklist

Run against live `docker compose up --build` stack.

---

## 1. NoTextExtracted UX (PIPE-05, D-21)

**Scenario:** Upload an image-only PDF with no embedded text (e.g. a scanned photo stored as PDF without OCR layer).

**Steps:**
1. Log in and navigate to `/upload`.
2. Upload the image-only PDF.
3. Wait for per-file card status to transition.
4. Open DevTools Network → filter for `/status`.

**Expected:**
- Per-file card shows `Fehlgeschlagen` badge.
- Alert with "Aus diesem Dokument konnte kein Text extrahiert werden. Bitte laden Sie eine PDF-Datei mit Textinhalt hoch oder versuchen Sie ein klares Foto."
- Network `/status` response body: `errorCode: "NoTextExtracted"`.
- DB check: `SELECT error_message FROM processing_runs WHERE ...` returns German string, NOT any internal exception text.

- [ ] Pass / Fail: ___

---

## 2. InsufficientTokens UX (PIPE-05)

**Scenario:** Set test user's token balance to 0; upload a multi-item PDF batch.

**Steps:**
1. Manually set `user_token_balances.balance = 0` for test user via psql.
2. Upload a valid PDF.
3. Wait for classification phase.

**Expected:**
- Status transitions through `Queued` → `Classifying` → `Fehlgeschlagen`.
- Alert text contains "Ihr Token-Guthaben reicht für diesen Beleg nicht aus. Bitte laden Sie Credits auf…"
- `errorCode: "InsufficientTokens"` in `/status` response.

- [ ] Pass / Fail: ___

---

## 3. AI Failure UX (PIPE-05)

**Scenario:** Simulate Anthropic API unavailability by setting `Anthropic__BaseUrl` to a black-hole URL (e.g. `http://127.0.0.1:9999`).

**Steps:**
1. Stop stack, set `Anthropic__BaseUrl=http://127.0.0.1:9999` in `.env`.
2. Restart stack, upload a valid PDF.
3. Wait for status changes.

**Expected:**
- Card transitions to `Fehlgeschlagen`.
- Alert: "Die Klassifizierung ist vorübergehend nicht verfügbar. Wir versuchen es automatisch erneut…"
- `errorCode: "AiUnavailable"` in `/status`.
- Backend Serilog logs contain the full `HttpRequestException` (not just the code).
- No internal IP/hostname in processing_runs.error_message column.

- [ ] Pass / Fail: ___

---

## 4. Cancel During Extracting (PIPE-06, D-14)

**Scenario:** Upload an OCR-heavy image file; click Cancel before Classifying.

**Steps:**
1. Upload a large image receipt.
2. While card shows `Text wird erkannt` (Extracting) or `Wird klassifiziert`, click "Abbrechen".

**Expected:**
- API returns 204.
- Toast: "Vorgang abgebrochen."
- Card transitions to `Abgebrochen` badge.
- Cancel button disappears (terminal state).
- Token ledger: no new debit entry for this file (`SELECT * FROM token_transactions WHERE …`).

- [ ] Pass / Fail: ___

---

## 5. Cancel of Terminal-State File (PIPE-06)

**Scenario:** Attempt to cancel an already-Completed file.

**Steps:**
1. Upload a file and wait for `Fertig` status.
2. In DevTools, POST `/api/v1/receipt-files/{id}/cancel` manually.

**Expected:**
- API returns 409.
- Toast: "Beleg ist bereits fertig verarbeitet — Abbruch nicht möglich."
- No status change in DB.

- [ ] Pass / Fail: ___

---

## 6. Polling Steady-State (PIPE-06, D-13)

**Scenario:** Verify polling stops once all files are terminal; restarts on new upload.

**Steps:**
1. Ensure all receipt files are Completed/Failed/Cancelled.
2. Navigate to `/receipts`.
3. Open DevTools Network → observe XHR/fetch requests.
4. Upload a new file.
5. Observe network activity while file processes.
6. Wait for file to complete; observe network again.

**Expected:**
- With all files terminal: ZERO `/status` polling requests.
- After new upload: 2s-cadence `/status` requests visible.
- After new file completes: polling stops again.

- [ ] Pass / Fail: ___

---

## 7. Dashboard Empty State (PIPE-06)

**Scenario:** Sign up a fresh account, navigate to Dashboard.

**Steps:**
1. Register a new account.
2. Navigate to `/` (Dashboard).

**Expected:**
- "Noch keine Belege vorhanden — laden Sie Ihren ersten Beleg hoch." message visible.
- No Skeleton stuck in rendering; no blank panel.
- DashboardStats tile area shows the empty-state card, not blank tiles.

- [ ] Pass / Fail: ___

---

## 8. Reports Empty State (PIPE-06)

**Scenario:** Same fresh account, navigate to `/reports`.

**Steps:**
1. Log in as fresh account (no receipts).
2. Navigate to `/reports`.

**Expected:**
- "Für dieses Jahr liegen noch keine bestätigten Belege vor. Bestätigen Sie zunächst Klassifizierungen unter Belege." copy visible.
- No blank panel; no Skeleton stuck.

- [ ] Pass / Fail: ___

---

## 9. Dashboard Error State (PIPE-06)

**Scenario:** API offline; verify error Alert appears on Dashboard.

**Steps:**
1. Run `docker compose stop api`.
2. Reload `/`.

**Expected:**
- Alert variant="destructive": "Daten konnten nicht geladen werden / Bitte versuchen Sie es erneut."
- "Erneut versuchen" button visible and functional (retries the query on click).
- No blank-screen-of-thinking.

- [ ] Pass / Fail: ___

---

## 10. Receipt Detail Skeleton During Processing (PIPE-06)

**Scenario:** Navigate to a receipt detail page while the file is still in Classifying status.

**Steps:**
1. Upload a valid PDF.
2. Quickly navigate to `/receipts/{receiptFileId}` while status is non-terminal.

**Expected:**
- Skeleton blocks render in place of the line-item table.
- No "Cannot read property of undefined" errors in console.
- No blank screen.
- Once status transitions to Completed, table appears automatically.

- [ ] Pass / Fail: ___
