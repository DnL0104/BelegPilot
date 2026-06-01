# Phase 6: Legal + Consent + Data Export + AVVs - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-01
**Phase:** 06-legal-consent-data-export
**Areas discussed:** Legal content sourcing, Cookie consent mechanism, Data export delivery, Audit log design

---

## Legal content sourcing

### Content production
| Option | Description | Selected |
|--------|-------------|----------|
| I draft full DE copy now | Complete German drafts for all four pages, tagged "Entwurf – Prüfung ausstehend"; lawyer sign-off in Phase 7 | ✓ |
| Generator tool output | User pastes eRecht24 / Dr. DSGVO output; Claude only wires it in | |
| Minimal stubs only | Scaffolding + TODOs; lawyer delivers all text | |

### Review gate
| Option | Description | Selected |
|--------|-------------|----------|
| Tracking doc + HUMAN-UAT item | `06-LEGAL-REVIEW.md` checklist + blocking UAT item, re-surfaced by Phase 7 QA-07 | ✓ |
| On-page review banner | Visible draft banner per page, removed on sign-off | |
| Both | Tracking doc + on-page banner | |

**User's choice:** Draft full DE copy now + tracking doc + HUMAN-UAT item.
**Notes:** An "Entwurf" marker still renders on-page (captured in CONTEXT specifics) even though the central tracking doc is the gate of record.

---

## Cookie consent mechanism

### Banner build
| Option | Description | Selected |
|--------|-------------|----------|
| Custom lightweight banner | React context + localStorage, no dependency | ✓ |
| OSS CMP library | klaro / vanilla-cookieconsent | |
| Commercial CMP | Usercentrics / Cookiebot | |

### Sentry gate reconciliation
| Option | Description | Selected |
|--------|-------------|----------|
| Env var = master switch, consent = runtime gate | NEXT_PUBLIC_SENTRY_ENABLED gates deploy; runtime consent gates init; close() on revoke | ✓ |
| Consent writes flag, reload | Stored flag + reload to re-init | |

### Consent categories
| Option | Description | Selected |
|--------|-------------|----------|
| Two: Notwendig + Fehleranalyse | Matches actual cookie use today | ✓ |
| Three (add Statistik) | Forward-looking analytics slot | |

**User's choice:** Custom banner; env-master + runtime-consent gate; two categories.
**Notes:** None.

---

## Data export delivery

### Delivery mechanism
| Option | Description | Selected |
|--------|-------------|----------|
| Async job + in-app download | Hangfire ExportUserDataJob → in-app "Bereit – Herunterladen"; no email infra | ✓ |
| Build email infra + emailed link | SMTP/provider + literal emailed link | |
| Both: in-app now + email optional | In-app primary, email if infra later | |

### Bundle storage
| Option | Description | Selected |
|--------|-------------|----------|
| Transient, expiring, auto-purged | /tmp bundle, expiring token, 24h purge job (honors FND-01) | ✓ |
| Generate on-demand, stream, never store | In-memory stream, no file | |

### Bundle contents
| Option | Description | Selected |
|--------|-------------|----------|
| JSON + CSV, zipped | receipts/items/classifications/token_transactions + README | ✓ |
| JSON only | Strict Art.20 machine-readable | |

**User's choice:** Async job + in-app download; transient expiring storage; JSON + CSV zip.
**Notes:** ⚠ Deliberate deviation from LEG-07's literal "emailed within 24h" — flagged in CONTEXT.md D-09 for planner/verifier.

---

## Audit log design

### Design / invocation
| Option | Description | Selected |
|--------|-------------|----------|
| Explicit IAuditLogger calls | Interface + explicit calls at each sensitive op | ✓ |
| EF SaveChanges interceptor | Automatic entity-change interception | |

### Schema / immutability
| Option | Description | Selected |
|--------|-------------|----------|
| Append-only, never edited/deleted | Retained indefinitely; actor survives account deletion | ✓ |
| Append-only with retention window | Cleanup purges >12 months | |

### Art.15 self-service scope
| Option | Description | Selected |
|--------|-------------|----------|
| Fold into the data export | Own audit entries in export bundle | ✓ |
| Dedicated audit-log view | Separate endpoint + UI panel | |
| Log only, defer retrieval | Table + logging now, no retrieval | |

**User's choice:** Explicit IAuditLogger; append-only indefinite; Art.15 folded into export bundle.
**Notes:** Couples audit logging to the export job — audit must exist before/with export (CONTEXT integration points).

---

## Claude's Discretion

- Exact shadcn components for banner + settings export panel.
- Exact German microcopy within the agreed structure.
- Zip/compression approach (System.IO.Compression).
- AuditAction enum value naming.
- Consent settings panel as dialog vs footer-anchored route.
- LEG-06 (AVVs/DPAs) + LEG-09 (Marken) handled as operator-tracked checklist artifacts (`06-AVV-TRACKING.md`, `06-MARKEN-SEARCH.md`) — not discussed as build decisions.

## Deferred Ideas

- Email/SMTP infrastructure — not built this milestone.
- Third "Statistik"/analytics consent category — until analytics exist.
- Dedicated user-facing audit-log view/endpoint — Art.15 met via export.
- Final pre-launch lawyer sign-off — Phase 7 (QA-07).
- BetterStack monitors + status-page link — Phase 7 (OBS-03, QA-06).
- Markenregister API integration — manual operator task.
