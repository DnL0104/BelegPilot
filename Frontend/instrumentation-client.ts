// Source: https://docs.sentry.io/platforms/javascript/guides/nextjs/manual-setup/
//        + https://nextjs.org/docs/app/api-reference/file-conventions/instrumentation-client
import * as Sentry from "@sentry/nextjs";
import { scrubEvent } from "@/lib/sentry-scrubber";

// instrumentation-client.ts runs after HTML load, before React hydration.
// localStorage is available here, but the ConsentProvider React context is not.
// Read consent from localStorage directly (Pattern 5 / Pitfall 5 in 06-RESEARCH.md).
const SENTRY_CONSENT_KEY = "taxreader-consent";

function hasSentryConsent(): boolean {
  try {
    const raw = localStorage.getItem(SENTRY_CONSENT_KEY);
    if (!raw) return false;
    const parsed = JSON.parse(raw) as { fehleranalyse?: boolean };
    return parsed.fehleranalyse === true;
  } catch {
    return false;
  }
}

// D-07: Sentry.init runs only when env-enabled AND the user has granted runtime consent.
if (process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true" && hasSentryConsent()) {
  Sentry.init({
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    environment: process.env.NEXT_PUBLIC_SENTRY_ENV ?? "production",
    sendDefaultPii: false,
    tracesSampleRate: 0,
    replaysOnErrorSampleRate: 0,
    replaysSessionSampleRate: 0,
    beforeSend(event) {
      return scrubEvent(event);
    },
  });
}

// Required by Next.js 16: capture router transitions for breadcrumbs.
export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
