// Source: https://docs.sentry.io/platforms/javascript/guides/nextjs/manual-setup/
//        + https://nextjs.org/docs/app/api-reference/file-conventions/instrumentation-client
import * as Sentry from "@sentry/nextjs";

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

const ALLOWED_QUERY_KEYS = new Set(["page", "pageSize", "year", "format"]);
const ALLOWED_HEADERS = new Set(["user-agent"]);
const UUID_RE =
  /\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/gi;

function scrubEvent(event: Sentry.ErrorEvent): Sentry.ErrorEvent | null {
  if (event.request) {
    delete event.request.data;

    if (typeof event.request.query_string === "string") {
      event.request.query_string = filterQueryString(event.request.query_string);
    }

    if (event.request.headers) {
      const keep: Record<string, string> = {};
      for (const [k, v] of Object.entries(event.request.headers)) {
        if (ALLOWED_HEADERS.has(k.toLowerCase())) {
          keep[k] = v as string;
        }
      }
      event.request.headers = keep;
    }

    if (typeof event.request.url === "string") {
      event.request.url = event.request.url.replace(UUID_RE, ":id");
    }
  }

  if (event.user) {
    delete event.user.email;
    delete event.user.username;
    delete event.user.ip_address;
    // Client cannot do async hashing here cheaply; drop the id outright.
    // Server-side capture in sentry.server.config.ts handles the hash if needed.
    delete event.user.id;
  }

  return event;
}

function filterQueryString(qs: string): string {
  const params = new URLSearchParams(qs);
  const filtered = new URLSearchParams();
  for (const [k, v] of params.entries()) {
    if (ALLOWED_QUERY_KEYS.has(k)) {
      filtered.append(k, v);
    }
  }
  return filtered.toString();
}
