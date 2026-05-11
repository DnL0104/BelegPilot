// Source: https://docs.sentry.io/platforms/javascript/guides/nextjs/manual-setup/
//        + https://nextjs.org/docs/app/api-reference/file-conventions/instrumentation-client
import * as Sentry from "@sentry/nextjs";
import { scrubEvent } from "@/lib/sentry-scrubber";

// D-16: Frontend Sentry stays disabled in production until Phase 6 wires
// the TTDSG cookie banner. We init only when the env flag is explicitly "true".
if (process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true") {
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
