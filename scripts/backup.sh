#!/bin/sh
# BKP-01/BKP-02: nightly backup to Backblaze B2 via restic.
# Runs inside the backup sidecar at 02:00 UTC (crontab entry below).
# RESTIC_REPOSITORY and RESTIC_PASSWORD are injected from docker-compose env.
# AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY are B2 Key ID / App Key.
#
# One-time init (run before first scheduled backup):
#   docker compose run --rm backup restic init
#
# crontab entry (add to /etc/crontabs/root inside the container):
#   0 2 * * * /bin/sh /backup.sh >> /var/log/backup.log 2>&1
set -e

# WR-02: signal the dead-man's-switch /fail endpoint on any non-zero exit, so monitoring
# distinguishes an explicit backup failure from a silently missed run. The success ping at
# the end hits the base URL; this trap only fires the /fail variant on error.
notify_fail() {
  rc=$?
  if [ "$rc" -ne 0 ] && [ -n "$HEALTHCHECKS_BACKUP_URL" ]; then
    curl -fsS --retry 3 "$HEALTHCHECKS_BACKUP_URL/fail" || true
  fi
}
trap notify_fail EXIT

echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Starting backup"

# Guard: init repo if it does not yet exist (idempotent on repeated runs)
restic snapshots > /dev/null 2>&1 || restic init

# BKP-01: PostgreSQL logical dump piped directly to restic stdin — no temp file on disk.
# Pitfall 7: pg_dump (not raw volume copy) — Postgres data files are not consistent
# while the server is running.
PGPASSWORD="$POSTGRES_PASSWORD" pg_dump \
  -h db -U "$POSTGRES_USER" belegpilot \
  | restic backup --stdin --stdin-filename db.sql

# BKP-02: receipt file uploads volume
restic backup /uploads

# D-08: retention — 7 daily + 4 weekly snapshots (~30-day worst-case window)
# --prune required: forget alone only removes snapshot metadata, not data blobs.
restic forget --keep-daily 7 --keep-weekly 4 --prune

echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] Backup complete"

# D-07: ping dead-man's-switch so a missed/failed run pages via ntfy
if [ -n "$HEALTHCHECKS_BACKUP_URL" ]; then
  curl -fsS --retry 3 "$HEALTHCHECKS_BACKUP_URL"
fi
