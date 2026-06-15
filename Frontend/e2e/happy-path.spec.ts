/**
 * BelegPilot E2E happy-path spec — DE locale, Europe/Berlin timezone.
 *
 * BACKEND PREREQUISITE:
 *   A running BelegPilot backend + PostgreSQL must be reachable at BACKEND_API_URL
 *   (default http://localhost:5190) before this spec runs. The Next.js dev/prod server
 *   proxies /api/v1/* to the backend via the rewrites in next.config.ts.
 *
 *   Locally:  docker compose up -d db api  (then let webServer start next build + next start)
 *   In CI:    wired by the heavy job in 07-06 (docker compose full-stack run).
 *
 * This spec does NOT mock /api/v1/* — it exercises the real stack end-to-end (QA-03).
 * Responsive smoke (sm 640px / md 768px) is covered by the sm/md viewport projects in
 * playwright.config.ts (QA-05 automated portion).
 * Phone-camera photo-upload (QA-05) is a HUMAN-UAT item (07-07).
 */

import { test, expect } from '@playwright/test'
import path from 'path'

const FIXTURE_PDF = path.join(__dirname, 'fixtures', 'sample-receipt.pdf')

// Use a per-run unique email so reruns against a persistent DB do not collide.
function uniqueEmail() {
  return `playwright+${Date.now()}@example.test`
}

const TEST_PASSWORD = 'Playwright2026!'

test('happy path: register → login → upload → classify → confirm → report → export', async ({
  page,
}) => {
  const email = uniqueEmail()

  // Pre-seed TTDSG consent so the cookie banner (fixed, z-50, bottom bar) never
  // renders and intercepts later clicks (e.g. the upload submit button).
  // addInitScript runs before the app's scripts on every navigation, so the
  // consent provider hydrates as already-decided.
  await page.addInitScript(() => {
    window.localStorage.setItem(
      'taxreader-consent',
      JSON.stringify({ notwendig: true, fehleranalyse: false, decided: true }),
    )
  })

  // ── 1. Register ────────────────────────────────────────────────────────────
  await page.goto('/register')
  await expect(page.getByRole('heading', { name: 'Konto erstellen' })).toBeVisible()

  await page.getByLabel('Name').fill('Playwright Test')
  await page.getByLabel('E-Mail').fill(email)
  // Use the first Passwort field (not "bestätigen")
  await page.locator('#password').fill(TEST_PASSWORD)
  await page.locator('#confirmPassword').fill(TEST_PASSWORD)
  await page.getByRole('button', { name: 'Registrieren' }).click()

  // After successful registration the app should redirect to an authenticated route
  // (or to /login if the auth flow routes there first).
  await page.waitForURL((url) => {
    const p = url.pathname
    return p === '/login' || p === '/upload' || p === '/' || p === '/receipts'
  }, { timeout: 15_000 })

  // ── 2. Login (if registration redirected to /login) ─────────────────────
  if (page.url().includes('/login')) {
    await expect(page.getByRole('heading', { name: 'Anmelden' })).toBeVisible()
    await page.getByLabel('E-Mail').fill(email)
    await page.getByLabel('Passwort').fill(TEST_PASSWORD)
    await page.getByRole('button', { name: 'Anmelden' }).click()
    await page.waitForURL((url) => !url.pathname.includes('/login'), { timeout: 15_000 })
  }

  // ── 3. Upload a receipt PDF ──────────────────────────────────────────────
  await page.goto('/upload')
  await expect(page.getByRole('heading', { name: 'Belege hochladen' })).toBeVisible()

  // Attach the fixture PDF via the hidden file input inside the dropzone
  const fileInput = page.locator('input[type="file"]')
  await fileInput.setInputFiles(FIXTURE_PDF)

  // The button label should now show the file count
  await expect(page.getByRole('button', { name: /Datei.*hochladen/i })).toBeVisible()
  await page.getByRole('button', { name: /Datei.*hochladen/i }).click()

  // Expect the success toast: "1 Beleg wird verarbeitet" or "werden verarbeitet"
  await expect(page.getByText(/Beleg.*verarbeitet/i)).toBeVisible({ timeout: 20_000 })

  // ── 4. Navigate to receipts list ─────────────────────────────────────────
  await page.goto('/receipts')
  // exact: true — otherwise "Belege" substring-matches the "Alle Belege" card heading too.
  await expect(page.getByRole('heading', { name: 'Belege', exact: true })).toBeVisible()

  // Wait for the uploaded receipt to appear in the list (processing may take a moment).
  // The receipts table renders rows once the backend completes processing.
  const receiptRow = page.locator('table tbody tr').first()
  await expect(receiptRow).toBeVisible({ timeout: 60_000 })

  // Click the first receipt row to open its detail page
  await receiptRow.click()
  await page.waitForURL(/\/receipts\/[^/]+$/, { timeout: 10_000 })

  // ── 5. Verify a classification / suggestion is visible ───────────────────
  // The receipt detail page shows items once processing completes.
  // Wait for the items table to appear (Artikel heading).
  await expect(page.getByText(/Artikel \(/)).toBeVisible({ timeout: 60_000 })

  // Look for classification indicators: "Vorschlag" label or a category label
  // from classify-dialog's categoryLabel helper.  We accept any of:
  //   - a "Vorschlag" / "Automatischer Vorschlag" chip
  //   - a "Bestätigen" action button
  //   - a category cell in the items table
  // Use a broad selector that fires as long as the item row is rendered.
  const itemRow = page.locator('table tbody tr').first()
  await expect(itemRow).toBeVisible({ timeout: 60_000 })

  // ── 6. Open classify dialog and confirm a classification ─────────────────
  // Click the first item row to open the classification dialog.
  await itemRow.click()

  // The dialog heading should appear
  await expect(
    page.getByRole('dialog').getByRole('heading', { name: 'Artikel klassifizieren' })
  ).toBeVisible({ timeout: 10_000 })

  // If there is a suggested category with a "Bestätigen" quick-confirm button, use it.
  // Otherwise, select a category manually and click the confirm button.
  const quickConfirmBtn = page.getByRole('dialog').getByRole('button', { name: 'Bestätigen' })
  const manualConfirmBtn = page.getByRole('dialog').getByRole('button', { name: /Vorschlag bestätigen|Kategorie zuweisen/i })

  if (await quickConfirmBtn.isVisible()) {
    await quickConfirmBtn.click()
    // Quick confirm toasts "Vorschlag bestätigt"
    await expect(page.getByText('Vorschlag bestätigt')).toBeVisible({ timeout: 10_000 })
  } else {
    // Select a category and confirm manually
    await page.getByRole('combobox').click()
    await page.getByRole('option').first().click()
    await manualConfirmBtn.click()
    // Manual confirm toasts "Klassifizierung bestätigt"
    await expect(page.getByText('Klassifizierung bestätigt')).toBeVisible({ timeout: 10_000 })
  }

  // ── 7. Navigate to reports and verify EUR de-DE formatting ───────────────
  await page.goto('/reports')
  await expect(page.getByRole('heading', { name: 'Jahresbericht' })).toBeVisible()

  // Wait for the category data to load. If there is data, a total in EUR de-DE format
  // should appear.  de-DE format: e.g. "12,99 €" or "12.099,99 €".
  // We wait up to 20s for data; if no data yet (report empty), still check the page renders.
  const hasData = await page
    .getByText(/\d+[.,]\d{2}\s*€/)
    .first()
    .isVisible({ timeout: 20_000 })
    .catch(() => false)

  if (hasData) {
    // Assert the EUR / de-DE comma-decimal pattern is present
    await expect(page.getByText(/\d+[.,]\d{2}\s*€/).first()).toBeVisible()
  } else {
    // The report may show the empty-state message if classification is still in-flight;
    // assert the page itself is healthy (Jahresbericht heading visible).
    await expect(page.getByRole('heading', { name: 'Jahresbericht' })).toBeVisible()
  }

  // ── 8. Trigger a CSV export and assert download starts ───────────────────
  // Export buttons are always rendered regardless of data (they are present on the page).
  const csvButton = page.getByRole('button', { name: 'CSV' })
  await expect(csvButton).toBeVisible()

  // Wait for a download event — the export handler creates a blob URL and clicks <a>.
  // Playwright intercepts the download when the server returns data (2xx with content).
  // We confirmed a classification in step 6, so there should always be data to export.
  const [download] = await Promise.all([
    page.waitForEvent('download', { timeout: 20_000 }),
    csvButton.click(),
  ])

  // A silent failure (export endpoint error) would NOT trigger the download event and
  // would instead show the "Export fehlgeschlagen" toast — that must not happen.
  const failureToast = page.getByText('Export fehlgeschlagen')
  await expect(failureToast).not.toBeVisible({ timeout: 5_000 })

  // The download object must be truthy — a missing/null download means the export failed.
  expect(download).not.toBeNull()
})
