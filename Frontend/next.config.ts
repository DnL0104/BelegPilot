import { networkInterfaces } from "node:os";
import type { NextConfig } from "next";
import { withSentryConfig } from "@sentry/nextjs";

const apiUrl = process.env.BACKEND_API_URL ?? "http://localhost:5190";

function isPrivateIpv4(address: string) {
  return (
    address.startsWith("10.") ||
    address.startsWith("192.168.") ||
    /^172\.(1[6-9]|2\d|3[0-1])\./.test(address)
  );
}

function getAllowedDevOrigins() {
  const origins = new Set(["localhost", "127.0.0.1"]);

  for (const addresses of Object.values(networkInterfaces())) {
    for (const address of addresses ?? []) {
      if (address.family !== "IPv4" || address.internal) {
        continue;
      }

      if (isPrivateIpv4(address.address)) {
        origins.add(address.address);
      }
    }
  }

  return [...origins];
}

const nextConfig: NextConfig = {
  // Standalone output is for the Docker image. The Playwright webServer sets
  // E2E_BUILD=true so the test build is a normal build and `next start` serves it
  // without the "next start does not work with output: standalone" warning.
  output: process.env.E2E_BUILD === "true" ? undefined : "standalone",
  allowedDevOrigins: getAllowedDevOrigins(),
  async rewrites() {
    return [
      {
        source: "/api/v1/:path*",
        destination: `${apiUrl}/api/v1/:path*`,
      },
    ];
  },
};

// D-16: only wrap with Sentry when the runtime flag is explicitly enabled.
// The wrap validates org/project at build time; skipping when disabled means
// CI doesn't need Sentry env vars set in Phase 1 (Pitfall 6).
export default process.env.NEXT_PUBLIC_SENTRY_ENABLED === "true"
  ? withSentryConfig(nextConfig, {
      silent: true,
      org: process.env.SENTRY_ORG,
      project: process.env.SENTRY_PROJECT,
      // Source-map upload deferred per CONTEXT.md <deferred> — do NOT pass authToken.
    })
  : nextConfig;
