import { defineConfig, devices } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  // HTML report (uploaded as the playwright-report artifact on failure) plus a
  // trace, screenshot, and video retained on failure so CI failures are
  // diagnosable from the page state, not guesswork.
  reporter: process.env.CI ? [['html', { open: 'never' }], ['list']] : 'list',
  use: {
    baseURL: 'http://localhost:3000',
    locale: 'de-DE',
    timezoneId: 'Europe/Berlin',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'md', use: { viewport: { width: 768, height: 1024 } } },
    { name: 'sm', use: { viewport: { width: 640, height: 900 } } },
  ],
  webServer: {
    command: 'npm run build && npm run start',
    url: 'http://localhost:3000',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
})
