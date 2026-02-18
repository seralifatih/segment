# Storage Migration: Glossary JSON/LiteDB -> SQLite Primary

## Overview
This change makes SQLite the primary runtime glossary store.

- Primary runtime store: `glossary.sqlite` via `GlossarySqliteStore` (`Segment/Services/GlossarySqliteStore.cs`)
- Runtime service boundary: `IGlossaryStore` + `ITermCollection` (`Segment/Services/IGlossaryStore.cs`, `Segment/Services/ITermCollection.cs`)
- Existing app API preserved: `GlossaryService` remains the consumer-facing static surface (`Segment/Services/GlossaryService.cs`)
- JSON is now tooling-only import/export through `GlossaryJsonInteropService` (`Segment/Services/GlossaryJsonInteropService.cs`)

## Schema
Schema is versioned in `SchemaVersion`.

### v1
- `TermScope`
- `TermEntry`
- `TermConflict`

### v2
- `TermUsageLog`

Migration runner:
- `GlossarySqliteStore.RunSchemaMigrations` applies incremental upgrades in-order (`Segment/Services/GlossarySqliteStore.cs`)

## First-run backward compatibility migration
When SQLite has no term rows:
1. Try migrate from legacy LiteDB (`glossary.db`) including:
- profiles -> `TermScope`
- terms collections -> `TermEntry`
- `glossary_resolution_conflicts` -> `TermConflict`
2. If no LiteDB data is found, try legacy JSON (`Global/glossary.json`, `Projects/*.json`)
3. If neither exists, initialize empty/default scopes only.

All migration attempts produce non-sensitive migration events via structured logging:
- success: `glossary_storage_migration_success`
- failure: `glossary_storage_migration_failed`

Payloads include only counts, source type, and exception type; no raw terms/segments/keys are logged.

## ACID and durability
- SQLite writes are transaction-wrapped for scope/term/conflict/usage operations.
- Connection PRAGMAs set for durability/safety:
- `foreign_keys=ON`
- `journal_mode=WAL`
- `synchronous=FULL`
- Lookup-critical indexes are created for:
- `(scope_name, source_normalized)`
- `(source_normalized, domain_vertical, source_language, target_language)`
- conflict timestamp
- usage log by scope/time and source

## Telemetry/audit boundary (separate durability domains)
Glossary storage migration intentionally does not merge telemetry/audit stores.

- Glossary durability domain: `glossary.sqlite`
- Telemetry domain remains separate (`niche_telemetry.db`) (`Segment/Services/NicheTelemetryService.cs`)
- Compliance audit remains separate append-only JSONL (`compliance_audit.jsonl`) (`Segment/Services/ComplianceAuditService.cs`)

Rationale:
- Keeps operational blast radius small.
- Preserves existing export/reporting behavior and retention boundaries.
- Avoids coupling high-frequency telemetry writes to glossary transactional paths.

## Failure recovery behavior
If migration fails:
- SQLite schema still initializes to current version.
- Legacy source files are not modified.
- Failure is logged with sanitized metadata.
- App continues with empty/default glossary scopes (`Global`, `Default`) and can still import legacy JSON explicitly.

If an individual transactional write fails:
- Transaction is rolled back.
- No partial term/conflict/usage writes are committed.

## Tests
Added tests (`Segment.Tests/GlossarySqliteStoreMigrationTests.cs`):
- legacy JSON migration on first run
- schema upgrade from v1 -> v2
- conflict persistence/read path
- rollback/transaction safety on failed term upsert

## Gap-Report Alignment
This migration directly addresses the following architecture-gap bullets from `docs/architecture-gap-report.md`.

### P0 addressed
- `API keys stored plaintext in settings.json.`
Status: Not addressed by this storage migration (separate settings/key-management track).

- `In-place fallback bypasses terminology/QA guardrails and can inject unchecked output...`
Status: Not addressed by this storage migration (translation workflow track).

- `p95<700ms objective has no enforceable control path...`
Status: Not addressed by this storage migration (latency/routing track).

### P1 addressed
- `Audit trail lacks tamper-evidence/signature chain.`
Status: Partially prepared only: glossary conflicts/usages now in transactional SQLite; compliance audit JSONL integrity hardening remains separate.

- `Telemetry consent not enforced at emit time.`
Status: Not addressed by this storage migration (telemetry policy track).

- `Guardrail override path not bound to AllowGuardrailOverrides policy.`
Status: Not addressed by this storage migration (guardrail workflow track).

### Additional alignment from report body
- `Learning lemma extraction is model-dependent...`
Status: Not changed; however `TermUsageLog` and `TermConflict` now provide stronger persistence for future deterministic-governance controls.

- `Interoperability depth is low...`
Status: Neutral; JSON remains tooling path and is no longer runtime persistence.
