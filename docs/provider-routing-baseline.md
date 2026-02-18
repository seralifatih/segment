# Provider Abstraction and Baseline Routing

## Scope
Implemented pluggable translation provider abstraction and runtime routing with fallback chain support.

## Implemented Components

### Provider contract
- `ITranslationProvider` (`Segment/Services/ITranslationProvider.cs`)
- Required members:
- `TranslateAsync(request, context, cancellationToken)`
- `HealthCheckAsync(cancellationToken)`
- `SupportsStreaming`
- `SupportsGlossaryHints`

### Registry
- `ITranslationProviderRegistry` + `TranslationProviderRegistry` (`Segment/Services/TranslationProviderRegistry.cs`)
- Runtime registration and lookup by provider name.

### Orchestrator
- `TranslationProviderOrchestrator` (`Segment/Services/TranslationProviderOrchestrator.cs`)
- Handles:
- primary -> secondary fallback execution
- capability-based routing (`RequiresStreaming`, glossary-hint stripping)
- health-state transitions (`Unknown`/`Healthy`/`Degraded`/`Unhealthy`)
- health snapshot refresh endpoint logic

### Built-in providers
- `GoogleTranslationProvider`
- `OllamaTranslationProvider`
- `CustomTranslationProvider`
- file: `Segment/Services/TranslationProviders.cs`

### Translation service integration
- `TranslationService` now routes via registry/orchestrator (`Segment/Services/TranslationService.cs`)
- Existing behavior preserved as default:
- primary route still from `AiProvider`
- optional secondary route from new `SecondaryAiProvider` setting
- fallback disabled unless secondary configured
- New service-level health status endpoint:
- `TranslationService.GetProviderHealthStatusAsync(...)`

### Settings compatibility
- Added optional `SecondaryAiProvider` with default empty value:
- `Segment/Services/SettingsService.cs`
- Existing settings files continue to load without breaking.

## Glossary hint capability behavior
- Providers that do not support glossary hints receive request with empty `GlossaryHints`.
- Providers that support hints receive resolved hints.

## Tests
Added tests in `Segment.Tests/TranslationProviderOrchestratorTests.cs`:
- fallback behavior (primary failure -> secondary success)
- health failure transition (unhealthy -> healthy)
- capability-based streaming routing
- conditional glossary hint passing

Also validated existing provider policy tests still pass:
- `Segment.Tests/ProviderRoutingPolicyTests.cs`
- `Segment.Tests/ProviderResiliencePolicyTests.cs`

## Gap-Report Alignment
Source: `docs/architecture-gap-report.md`

### P0 bullets
- `In-place fallback bypasses terminology/QA guardrails and can inject unchecked output into external systems.`
Status: Not addressed in this PR.

- `API keys stored plaintext in settings.json.`
Status: Not addressed in this PR.

- `p95<700ms objective has no enforceable control path and conflicts with configured timeout/retry envelope.`
Status: Partially addressed foundation only:
- provider routing/fallback abstraction enables future latency-based fast-path policies, but no hard SLO enforcement is added here.

### P1 bullets
- `Telemetry consent not enforced at emit time.`
Status: Not addressed in this PR.

- `Guardrail override path in panel not bound to AllowGuardrailOverrides policy.`
Status: Not addressed in this PR.

- `Audit trail lacks tamper-evidence/signature chain.`
Status: Not addressed in this PR.

## Notes
This change intentionally focuses on interoperability/routing architecture and provider pluggability while preserving current production defaults.
