# Terminology Resolution

## Purpose
Deterministic terminology resolution for prompt-time term locks with layered scope precedence.

Implementation:
- Resolver: `Segment/Services/GlossaryResolverService.cs`
- Result metadata: `Segment/Models/TermResolutionResult.cs`
- Prompt integration: `Segment/Services/TranslationService.cs` (`ResolveLockedTerminology`)

## Deterministic Algorithm

Input:
- `sourceTerm`
- `TermResolutionContext` (`DomainVertical`, `SourceLanguage`, `TargetLanguage`, optional `ProjectId`, `TeamId`, `UserId`)

Rules (in order):
1. Exact match first:
- source term normalized exact equality
- exact domain match
- language pair match

2. Scope precedence:
- `Project > Team > User Global > System defaults`

3. Tie-break within same scope:
- Most recent `LastAcceptedAt` wins.

4. Low-confidence collision:
- If multiple candidates still tie after rule 3 (same scope + same recency), resolver returns explicit conflict result for UI:
- `IsLowConfidenceCollision = true`
- `RequiresUserSelection = true`
- `Winner = null`

Determinism guarantee:
- All candidate ordering paths use stable deterministic sort keys.
- Same inputs + same term set always produce same `TermResolutionResult`.

## Explainability Metadata
Resolver returns:
- `WinningRule`
- `ScopePrecedenceApplied`
- `Reason`
- `DecisionTrace` (rule-by-rule trace)
- collision flags: `IsLowConfidenceCollision`, `RequiresUserSelection`

## Prompt Lock Integration
`TranslationService.ResolveLockedTerminology`:
- Runs resolver for each matched source term in the input segment.
- Applies lock only when resolver returns a concrete winner.
- Skips locks for low-confidence collisions and emits `terminology_resolution_collision` structured event with non-sensitive metadata.

This ensures enforced term locks for deterministic resolutions while surfacing ambiguous cases instead of silently applying unstable mappings.

## Examples

### Example A: precedence
Candidates for `agreement`:
- Project: `project-term`
- Team: `team-term`
- User: `user-global-term`
- System: `system-default-term`
Result: `project-term` (rule 2)

### Example B: tie-break
Two Team candidates for `indemnification`:
- `LastAcceptedAt=2026-01-01` and `2026-02-01`
Result: newer timestamp wins (rule 3)

### Example C: domain override
`claim` exists in Legal and Patent domains.
Context domain = Patent.
Result: Patent term selected (rule 1)

### Example D: low-confidence collision
Two Team candidates for `governing law` share identical `LastAcceptedAt`.
Result: `Winner=null`, `RequiresUserSelection=true` (rule 4)

## Tests
Coverage file: `Segment.Tests/GlossaryResolverServiceTests.cs`
- precedence
- tie-breaking
- domain-specific override
- low-confidence collision surfacing

## Gap-Report Alignment
Source: `docs/architecture-gap-report.md`

### P0 bullets
- `In-place fallback bypasses terminology/QA guardrails and can inject unchecked output into external systems.`
Status: Not addressed by this resolver change.

- `API keys stored plaintext in settings.json.`
Status: Not addressed by this resolver change.

- `p95<700ms objective has no enforceable control path...`
Status: Not addressed by this resolver change.

### P1 bullets
- `Telemetry consent not enforced at emit time.`
Status: Not addressed by this resolver change.

- `Guardrail override path in panel not bound to AllowGuardrailOverrides policy.`
Status: Not addressed by this resolver change.

- `Audit trail lacks tamper-evidence/signature chain.`
Status: Not addressed by this resolver change.

### Deterministic terminology gaps addressed (from requirement section)
- Addressed: deterministic, explainable scope/recency resolution behavior.
- Addressed: explicit low-confidence collision surfacing for UI prompt workflows.
- Not addressed: in-place bypass/override-policy workflow issues (separate control-plane changes).
