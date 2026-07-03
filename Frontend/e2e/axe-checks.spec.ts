/**
 * BelegPilot WCAG 2.1 AA accessibility gate — axe-core scan for every redesigned route.
 *
 * Wave 0 scaffold (04-01-PLAN.md Task 3): this spec is EXPECTED to be red until each
 * screen is redesigned in later Phase 4 waves. That is correct for a Wave 0 gate — it
 * exists so later waves have an automated a11y regression check to turn green against.
 *
 * BACKEND PREREQUISITE: same as happy-path.spec.ts — a running BelegPilot backend +
 * PostgreSQL must be reachable at BACKEND_API_URL (default http://localhost:5190).
 *
 * Coverage gap: /receipts/[id] (dynamic route) is intentionally OUT OF SCOPE for this
 * scaffold — it needs a fixture receipt with completed classification. Track separately;
 * add a fixture-backed axe test for it in a later wave.
 */

import { test, expect, type Page } from '@playwright/test'
import AxeBuilder from '@axe-core/playwright'

/** WCAG 2.1 AA tag groups per 04-UI-SPEC.md § @axe-core/playwright Integration. */
const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']

// Use a per-run unique email so reruns against a persistent DB do not collide, and so
// this spec's auth state never collides with happy-path.spec.ts's own unique-email user.
function uniqueEmail() {
  return `axe+${Date.now()}@example.test`
}

const TEST_PASSWORD = 'Playwright2026!'

/** Pre-seed TTDSG consent so the cookie banner (fixed, bottom bar) never renders and
 * never gets flagged as an extra axe target mid-scan. */
async function preSeedConsent(page: Page) {
  await page.addInitScript(() => {
    window.localStorage.setItem(
      'taxreader-consent',
      JSON.stringify({ notwendig: true, fehleranalyse: false, decided: true }),
    )
  })
}

/** Registers a fresh per-run user and lands authenticated. Reused by every
 * authenticated-route test below — mirrors happy-path.spec.ts's register→login flow. */
async function registerAndLogin(page: Page): Promise<void> {
  const email = uniqueEmail()
  await preSeedConsent(page)

  await page.goto('/register')
  await page.getByLabel('Name').fill('Axe Test')
  await page.getByLabel('E-Mail').fill(email)
  await page.locator('#password').fill(TEST_PASSWORD)
  await page.locator('#confirmPassword').fill(TEST_PASSWORD)
  await page.getByRole('button', { name: 'Registrieren' }).click()

  await page.waitForURL(
    (url) => {
      const p = url.pathname
      return p === '/login' || p === '/upload' || p === '/' || p === '/receipts'
    },
    { timeout: 15_000 },
  )

  if (page.url().includes('/login')) {
    await page.getByLabel('E-Mail').fill(email)
    await page.getByLabel('Passwort').fill(TEST_PASSWORD)
    await page.getByRole('button', { name: 'Anmelden' }).click()
    await page.waitForURL((url) => !url.pathname.includes('/login'), { timeout: 15_000 })
  }
}

async function assertNoWcagViolations(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(WCAG_TAGS).analyze()
  expect(results.violations).toEqual([])
}

// ── Authenticated routes ──────────────────────────────────────────────────────────
const AUTHENTICATED_ROUTES: Array<{ path: string; description: string }> = [
  { path: '/', description: 'Übersicht (/)' },
  { path: '/upload', description: 'Belege hochladen (/upload)' },
  { path: '/receipts', description: 'Meine Belege (/receipts)' },
  { path: '/reports', description: 'Berichte (/reports)' },
  { path: '/billing', description: 'Credits & Abrechnung (/billing)' },
  { path: '/settings', description: 'Einstellungen (/settings)' },
]

for (const route of AUTHENTICATED_ROUTES) {
  test(`${route.description} — keine WCAG 2.1 AA-Verletzungen`, async ({ page }) => {
    await registerAndLogin(page)
    await page.goto(route.path)
    await expect(page).toHaveTitle(/BelegPilot/)
    await assertNoWcagViolations(page)
  })
}

// ── Unauthenticated routes ────────────────────────────────────────────────────────
const PUBLIC_ROUTES: Array<{ path: string; description: string }> = [
  { path: '/login', description: 'Anmelden (/login)' },
  { path: '/register', description: 'Registrieren (/register)' },
  { path: '/impressum', description: 'Impressum (/impressum)' },
  { path: '/datenschutz', description: 'Datenschutzerklärung (/datenschutz)' },
]

for (const route of PUBLIC_ROUTES) {
  test(`${route.description} — keine WCAG 2.1 AA-Verletzungen`, async ({ page }) => {
    await preSeedConsent(page)
    await page.goto(route.path)
    await expect(page).toHaveTitle(/BelegPilot/)
    await assertNoWcagViolations(page)
  })
}
