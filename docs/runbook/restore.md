# Backup Restore Procedure (BKP-03)

**Purpose:** Verify that the nightly restic backups are recoverable. This runbook documents the standard restore procedure and must be executed at least once before commercial launch (BKP-03). Results are recorded in the Drill Log below.

**Target:** RTO < 4 hours / RPO < 24 hours (D-09).

---

## Prerequisites

- restic installed locally (or run the restore inside the backup sidecar container)
- Docker available on the restore host
- B2 credentials (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`) and `RESTIC_REPOSITORY` / `RESTIC_PASSWORD` available

---

## Restore Procedure

### Step 1 — Start a throwaway Postgres container

```bash
docker run --name pg-restore-test -d \
  -e POSTGRES_PASSWORD=testpass \
  -e POSTGRES_DB=belegpilot \
  postgres:17-alpine
```

Wait ~5 seconds for Postgres to be ready.

### Step 2 — List available snapshots

```bash
restic snapshots
```

Note the snapshot ID you want to restore (typically `latest`).

### Step 3 — Restore the DB dump from restic

```bash
restic restore latest \
  --include db.sql \
  --target /tmp/restore-test
```

This places `db.sql` at `/tmp/restore-test/db.sql`.

### Step 4 — Load the dump into the throwaway database

```bash
PGPASSWORD=testpass psql \
  -h localhost -p 5432 -U postgres belegpilot \
  < /tmp/restore-test/db.sql
```

### Step 5 — Run COUNT(*) integrity checks

```bash
PGPASSWORD=testpass psql -h localhost -p 5432 -U postgres belegpilot \
  -c "SELECT COUNT(*) FROM users; SELECT COUNT(*) FROM receipt_files; SELECT COUNT(*) FROM receipt_items; SELECT COUNT(*) FROM payments;"
```

All four tables must return COUNT > 0 for a production data restore (COUNT = 0 for a fresh/empty backup is also acceptable if the data matches the source DB state). Record the exact counts in the Drill Log below.

### Step 6 — Verify uploaded files (BKP-02)

```bash
restic restore latest \
  --include /uploads \
  --target /tmp/restore-test-uploads
ls /tmp/restore-test-uploads/uploads/
```

Confirm uploaded receipt files are present.

### Step 7 — Tear down

```bash
docker rm -f pg-restore-test
rm -rf /tmp/restore-test /tmp/restore-test-uploads
```

---

## RESTIC_PASSWORD Recovery Warning

**D-06:** The `RESTIC_PASSWORD` is the sole decryption key for all backups. If it is lost, all backup data is permanently unrecoverable. Ensure:

- The password is stored in a password manager (e.g., Bitwarden, 1Password).
- An offline paper or encrypted USB copy exists separately from the server.
- Consider adding a recovery key: `restic key add` (prompts for an additional passphrase).

---

## Drill Log

Record results each time the restore drill is executed.

### Drill 1 (First Execution — required before launch)

| Field | Value |
|-------|-------|
| Drill date | ___________________ |
| Snapshot ID used | ___________________ |
| Snapshot timestamp (RPO check) | ___________________ |
| RTO start time | ___________________ |
| RTO end time | ___________________ |
| RTO elapsed | ___________________ |
| RPO (time since last backup) | ___________________ |
| COUNT(*) users | ___________________ |
| COUNT(*) receipt_files | ___________________ |
| COUNT(*) receipt_items | ___________________ |
| COUNT(*) payments | ___________________ |
| Uploads volume files present | Yes / No |
| Result | PASS / FAIL |
| Notes | ___________________ |

**RTO target:** < 4 hours
**RPO target:** < 24 hours (equal to the backup interval)

---

### Subsequent Drills

| Date | Snapshot ID | RTO Elapsed | RPO | Count Check | Result |
|------|-------------|-------------|-----|-------------|--------|
| | | | | | |

---

## Troubleshooting

**"Fatal: unable to open config file"** — The restic repository has not been initialized. Run `docker compose run --rm backup restic init` once before the first backup.

**psql connection refused** — The throwaway container is not yet ready. Wait a few seconds and retry.

**COUNT(*) = 0 on all tables** — The dump loaded but the database is empty. Check that `pg_dump` ran against the correct database (`belegpilot`) and user. Verify the snapshot was taken after data was written.

**restic restore exits non-zero** — Check `RESTIC_REPOSITORY` and `RESTIC_PASSWORD` env vars. Run `restic snapshots` to confirm the repository is accessible.
