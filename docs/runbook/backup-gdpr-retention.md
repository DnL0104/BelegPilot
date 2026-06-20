# Backup Retention and GDPR Reconciliation (BKP-04)

**Purpose:** Document how the restic backup retention policy interacts with GDPR deletion requests. This engineering disclosure feeds the Phase 3 Datenschutzerklärung (privacy policy).

---

## Retention Policy

The nightly backup job runs `restic forget --keep-daily 7 --keep-weekly 4 --prune`.

This retains:

- 7 daily snapshots (one per day for the last 7 days)
- 4 weekly snapshots (one per week for the last 4 weeks)

The `--prune` flag ensures data blobs are physically removed from Backblaze B2 after `forget` removes the snapshot references. Without `--prune`, storage would grow indefinitely.

**Worst-case retention window:** approximately 30 days.

The exact worst case is 4 weekly snapshots covering approximately 28 days, plus the 7-day daily overlap — meaning a backup taken just before a GDPR deletion request could persist for up to ~30 days before it is removed by the retention policy.

---

## GDPR Deletion Interaction

When a user exercises their right to erasure (Art. 17 GDPR) or deletes their account:

1. **Immediate deletion:** All live database rows for the user (account, receipts, items, classifications, token history, payments, audit log) are deleted immediately via the `DeleteAccountHandler` cascade. The user's data is no longer accessible through the application.

2. **Backup persistence:** Backup snapshots taken before the deletion request still contain the deleted data. These snapshots are not individually modified — restic backups are immutable. The data remains in encrypted form on Backblaze B2 for up to ~30 days, until the retention policy's `forget --prune` cycle purges the relevant snapshots.

3. **Encryption:** All data at rest in B2 is encrypted with AES-256 by restic (client-side). Backblaze B2 never sees plaintext. The `RESTIC_PASSWORD` is the sole decryption key.

4. **Propagation timeline:** Under normal operation (daily backup + retention enforcement), the user's data will be absent from all remaining snapshots within 30 days of the deletion request.

---

## Disclosure Requirement for Phase 3

The Phase 3 Datenschutzerklärung (privacy policy) must disclose that:

- Deleted user data may persist in encrypted backup snapshots for up to **30 days** following account deletion or a deletion request.
- This retention is a technical consequence of the backup rotation policy (7 daily + 4 weekly snapshots).
- Backups are encrypted and inaccessible to Backblaze B2 (the storage provider).
- After the ~30-day window, deleted data is purged from backups automatically.

The legal copy for this disclosure is authored in Phase 3. This document provides the engineering facts to inform that copy — it is not a legal document.

---

## Related

- `docs/runbook/restore.md` — restore procedure (BKP-03)
- `scripts/backup.sh` — backup script with the `restic forget --keep-daily 7 --keep-weekly 4 --prune` call
- CONTEXT.md D-08 — retention window engineering decision
- REQUIREMENTS.md BKP-04 — requirement traceability
