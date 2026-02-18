# GTM Hardening Release Checklist

## 1) Phase-Gate Readiness Criteria
- `Onboarding`: onboarding funnel ingestion is resilient (retry + idempotent correlation IDs) and phase qualification is active.
- `Referral attribution`: referral registration/milestones enforce registered-user integrity and reward eligibility is deterministic.
- `KPI ingestion`: PMF event ingestion is idempotent via ingestion keys and normalizes invalid/future timestamps.
- `Billing/entitlement sync`: account-level billing + entitlement checksum sync is operational and idempotent.
- `Observability`: dashboards available for onboarding drop-off, referral conversion funnel, and pilot conversion funnel.
- `Compliance`: audit export and data-handling disclosures are available, with confidential local-only enforcement active when configured.

## 2) Rollback Plan
- Stop rollout by disabling new GTM workflows in release toggles (onboarding intake campaigns and paid pilot conversion nudges).
- Revert application package to last stable build artifact.
- Restore previous config snapshot:
  - `settings.json`
  - `gtm_config.db`
- Preserve data stores (`*.db`) and only roll back application binaries first.
- Validate rollback health:
  - App startup success
  - Translation routing operational
  - Existing billing resolution unchanged

## 3) Support Runbook
- Triage order:
  1. Production crash/blocker incidents
  2. Billing/entitlement mismatches
  3. Onboarding/referral attribution integrity issues
  4. Dashboard/reporting discrepancies
- Common checks:
  - Confirm active launch phase and cohort rules in GTM config.
  - Verify referral user registration exists before milestones.
  - Inspect PMF ingestion keys for duplicate event attempts.
  - Verify billing sync checksum changes only when selection/entitlements change.
- Evidence exports:
  - Compliance audit log export (`csv`/`jsonl`)
  - PMF weekly snapshot export (`csv`/`pdf`)
  - Co-branded pilot outcomes and glossary reports

## 4) Incident Response Contacts
- Incident Commander (IC): GTM Engineering Lead
- Deputy IC: Product Engineering Lead
- Communications Lead: Founder / GTM Owner
- Customer Response Lead: Support Operations Lead
- Escalation SLA:
  - Sev-1: immediate paging, 15-minute response target
  - Sev-2: 1-hour response target
  - Sev-3: next-business-day response target

## 5) Final Go/No-Go
- Full test suite passing on release candidate build.
- No unresolved Sev-1 / Sev-2 defects.
- Rollback rehearsal completed and documented.
- Support + incident contacts confirmed for release window.
