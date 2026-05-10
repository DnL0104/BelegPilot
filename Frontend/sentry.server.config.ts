import * as Sentry from "@sentry/nextjs";

if (process.env.SENTRY_DSN_FRONTEND_SERVER) {
  Sentry.init({
    dsn: process.env.SENTRY_DSN_FRONTEND_SERVER,
    environment: process.env.SENTRY_ENV ?? "production",
    sendDefaultPii: false,
    tracesSampleRate: 0,
  });
}

export {};
