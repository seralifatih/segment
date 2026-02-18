# Release Readiness Checklist

This checklist translates MVP hardening into launch decision gates for PM + Engineering.

## Security/Privacy

### Must
- [ ] API keys are only stored via DPAPI secret store; `settings.json` contains no plaintext provider secrets.
- [ ] Telemetry opt-in enforcement is validated for usage/crash/model-output paths.
- [ ] Confidentiality mode policy is enforced (`LocalOnly`, approved provider blocking).
- [ ] Prompt/payload redaction checks pass for sensitive patterns before external logging.
- [ ] Audit-log integrity gate passes:
  - hash-chain verification
  - signature checkpoint verification (when signing key is configured)
  - command: `pwsh ./scripts/release-readiness-gate.ps1`

### Should
- [ ] Account-level telemetry consent lock is tested for cross-account mutation attempts.
- [ ] Crash logs include redaction and minimize-logging behavior in privacy mode.

### Could
- [ ] Periodic key-rotation rehearsal documented for provider credentials.

## Reliability/Ops

### Must
- [ ] Latency gate passes: short-segment `p95 <= 700ms`.
- [ ] Crash-free session gate passes: `>= 99.5%` over release window.
- [ ] Glossary determinism gate passes: deterministic resolver pass rate `>= 99.5%`.
- [ ] Migration gate passes: first-run glossary migration success `>= 99.5%`.
- [ ] Release-gate tests pass (must-set):
  - audit integrity
  - determinism resolver
  - migration path
  - latency percentile computation

### Should
- [ ] Guardrail warning/strict mode behavior is validated on legal/regulatory profiles.
- [ ] Undo-safe paste + timeout recovery scenarios are exercised in RC smoke run.

### Could
- [ ] Canary telemetry dashboard snapshots archived for pre/post deploy comparison.

## Distribution/Update Safety

### Must
- [ ] Installer/update path preserves existing settings, glossary SQLite, and audit artifacts.
- [ ] Update rollback path is validated on one prior released build.
- [ ] No destructive schema downgrade path is required for rollback (forward-only migration documented).

### Should
- [ ] Update package integrity/signature verification is checked in staging rollout.
- [ ] Hotkey migrations keep backward-compatible defaults and conflict detection.

### Could
- [ ] Delta update payload size budget tracked and reported for release notes.

## Compliance UX Gates

### Must
- [ ] Blocking guardrails require explicit override reason.
- [ ] QA warning summary appears pre-apply with keyboard actions (`R` review, `A` apply anyway).
- [ ] Strict QA mode available and honored for legal/regulatory workflows.
- [ ] Learning flow has explicit consent options and no silent glossary overwrite.

### Should
- [ ] Weekly digest includes learned terms and unresolved conflicts for reviewer follow-up.
- [ ] Compliance export (CSV/JSONL) validated from settings UI on release candidate.

### Could
- [ ] Policy banners/tooltips localized for top launch languages.

## Measurable Launch Gates

| Gate | Target | Must/Should/Could | Source |
|---|---:|---|---|
| Latency p95 (short segments) | `<= 700 ms` | Must | `docs/release-metrics.json`, `ReflexLatencyMetricsService` snapshot |
| Crash-free session rate | `>= 0.995` | Must | release metrics feed |
| Glossary determinism pass rate | `>= 0.995` | Must | deterministic resolver test/metrics |
| Migration success rate | `>= 0.995` | Must | migration logs + integration tests |
| Audit integrity verification | pass/fail | Must | hash chain + checkpoint signature gate |

## CI/Automation

- Gate script: `scripts/release-readiness-gate.ps1`
- Example command:
  - `pwsh ./scripts/release-readiness-gate.ps1 -MetricsFile docs/release-metrics.json -AuditBasePath "$env:APPDATA\\SegmentApp"`
- Script enforces:
  - release-gate tests
  - audit hash-chain/signature checkpoint verification
  - measurable threshold gates (latency/crash-free/determinism/migration)

## Release Decision Rule

- **Go**: all Must items checked and all measurable Must gates passing.
- **No-Go**: any Must item unchecked or any Must gate failing.
- **Conditional Go**: Must gates pass, but Should items have documented owner + date in release notes.
