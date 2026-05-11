// D-14 PII scrubber shared across all three Sentry runtimes (client, server, edge).
// Strips request bodies, disallowed headers, query keys, UUIDs in URLs, and user PII.
import type * as Sentry from "@sentry/nextjs";

const ALLOWED_QUERY_KEYS = new Set(["page", "pageSize", "year", "format"]);
const ALLOWED_HEADERS = new Set(["user-agent"]);
const UUID_RE =
  /\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/gi;

export function scrubEvent(
  event: Sentry.ErrorEvent,
): Sentry.ErrorEvent | null {
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
