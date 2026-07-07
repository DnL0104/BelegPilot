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

/** Seeds a page with a pre-authenticated session (localStorage `user` + `refreshToken`,
 * matching what `AuthProvider` writes on real login/register — see auth-provider.tsx) plus
 * consent, so navigating to a protected route lands authenticated without a fresh
 * `/auth/register` call. The axios interceptor transparently exchanges the refresh token
 * for an access token on the first 401 (api-client.ts). */
async function seedAuthenticatedSession(page: Page, session: AuthSession): Promise<void> {
  await preSeedConsent(page)
  await page.addInitScript((s) => {
    window.localStorage.setItem('refreshToken', s.refreshToken)
    window.localStorage.setItem('user', JSON.stringify(s.user))
  }, session)
}

async function assertNoWcagViolations(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(WCAG_TAGS).analyze()
  expect(results.violations).toEqual([])
}

interface AuthSession {
  refreshToken: string
  user: { id: string; email: string; displayName: string }
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

// Register ONE shared user for the whole authenticated-route battery instead of one
// per route. `/auth/register` sits behind the `auth-strict` rate-limit policy (5/min,
// see Program.cs) — registering fresh for all 6 AUTHENTICATED_ROUTES tests reliably
// trips that limiter on the 6th call and makes the gate flaky-by-design. A single
// register + localStorage-seeded session (mirrors what AuthProvider persists on a
// real login) keeps every route test isolated at the page level while making exactly
// one `/auth/register` call for this describe block.
test.describe('Authenticated routes', () => {
  let session: AuthSession

  test.beforeAll(async ({ request }) => {
    const response = await request.post('/api/v1/auth/register', {
      data: {
        email: uniqueEmail(),
        displayName: 'Axe Test',
        password: TEST_PASSWORD,
      },
    })
    const body = await response.json()
    session = { refreshToken: body.refreshToken, user: body.user }
  })

  for (const route of AUTHENTICATED_ROUTES) {
    test(`${route.description} — keine WCAG 2.1 AA-Verletzungen`, async ({ page }) => {
      await seedAuthenticatedSession(page, session)
      await page.goto(route.path)
      await expect(page).toHaveTitle(/BelegPilot/)
      await assertNoWcagViolations(page)

      // Refresh tokens are single-use/rotating (RefreshTokenService). The seeded
      // token gets rotated by api-client.ts's own 401-refresh interceptor on this
      // page's first authenticated call — it's the only thing allowed to rotate it;
      // a second, test-side rotation would race it and replay an already-used token,
      // getting revoked and hard-redirecting to /login mid-scan. So instead of
      // rotating ourselves, harvest whatever token the page's own refresh left in
      // localStorage and hand it to the next (isolated-context) test.
      const rotated = await page.evaluate(() => window.localStorage.getItem('refreshToken'))
      if (rotated) session = { ...session, refreshToken: rotated }
    })
  }
})

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
