# syntax=docker/dockerfile:1.7
FROM node:22-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci

FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
ENV NEXT_TELEMETRY_DISABLED=1
# Next.js bakes rewrite destinations into the standalone manifest at build time.
# BACKEND_API_URL MUST be set BEFORE `npm run build` so /api/v1/* rewrites
# resolve to http://api:8080 in the Docker Compose network rather than the
# http://localhost:5190 dev fallback in next.config.ts. Override via
# `--build-arg BACKEND_API_URL=...` for non-default deployments.
ARG BACKEND_API_URL=http://api:8080
ENV BACKEND_API_URL=${BACKEND_API_URL}
RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000
ENV HOSTNAME=0.0.0.0

# Non-root user for safety
RUN addgroup -S nodejs && adduser -S nextjs -G nodejs

COPY --from=build --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=build --chown=nextjs:nodejs /app/.next/static ./.next/static
COPY --from=build --chown=nextjs:nodejs /app/public ./public

USER nextjs
EXPOSE 3000
CMD ["node", "server.js"]
