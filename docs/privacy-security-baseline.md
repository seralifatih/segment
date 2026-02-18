# Privacy & Security Baseline

## Scope
This baseline implements enterprise privacy controls for secrets, telemetry/logging, and confidentiality routing behavior.

## Threat Surfaces
- API credential exposure in plaintext config files.
- Sensitive payload leakage in diagnostic/crash logs.
- Telemetry emissions without explicit consent checks.
- Non-approved provider usage in regulated environments.
- Cloud routing when local/strict confidentiality behavior is required.

## Implemented Mitigations

### 1) Secret Storage (DPAPI, no plaintext config)
- Added DPAPI-backed secret store:
  - `Segment/Services/ISecretStore.cs`
  - `Segment/Services/DpapiSecretStore.cs`
- `SettingsService` now:
  - migrates legacy plaintext API keys from `settings.json` into DPAPI store on load
  - logs migration success/failure without secret payloads
  - always persists `settings.json` with `GoogleApiKey` and `CustomApiKey` blank
  - `Segment/Services/SettingsService.cs`

### 2) Confidentiality Controls
- Added config controls:
  - `MinimizeDiagnosticLogging`
  - `EnforceApprovedProviders`
  - `ApprovedProvidersCsv`
  - `PreferLocalProcessingPath`
  - `Segment/Services/SettingsService.cs`
- Added settings UI wiring:
  - `Segment/Views/SettingsWindow.xaml`
  - `Segment/Views/SettingsWindow.xaml.cs`
- Routing policy enforcement:
  - optional local-first hook routes to Ollama (`PreferLocalProcessingPath`)
  - block route if provider is not approved (`EnforceApprovedProviders` + `ApprovedProvidersCsv`)
  - `Segment/Services/TranslationService.cs`

### 3) Redaction Before Logging/Diagnostics
- Added redaction utility for sensitive patterns:
  - email, phone, SSN, card-like values, bearer token, API key/token/secret patterns, long numeric identifiers
  - `Segment/Services/SensitiveDataRedactor.cs`
- Structured diagnostics now use centralized redaction:
  - `Segment/Services/StructuredLogger.cs`

### 4) Consent Enforcement for Telemetry/Diagnostics
- Usage telemetry now hard-gated by consent:
  - `NicheTelemetryService.RecordEvent` returns when `TelemetryUsageMetricsConsent` is false
  - `Segment/Services/NicheTelemetryService.cs`
- Structured logging now enforces consent centrally:
  - `info` events require `TelemetryUsageMetricsConsent`
  - `error` events require `TelemetryCrashDiagnosticsConsent`
  - `Segment/Services/StructuredLogger.cs`

### 5) Crash Logging Policy
- Crash logging now:
  - respects `TelemetryCrashDiagnosticsConsent`
  - redacts sensitive values in message/stack/inner exception
  - minimizes stack/inner details in strict privacy mode (`MinimizeDiagnosticLogging` or local-only confidentiality)
  - `Segment/App.xaml.cs`

## Tests
- No plaintext API key persistence:
  - `Segment.Tests/SettingsServiceSecretPersistenceTests.cs`
- Redaction coverage for sensitive patterns:
  - `Segment.Tests/SensitiveDataRedactorTests.cs`
- Telemetry consent gate behavior:
  - `Segment.Tests/NicheTelemetryServiceTests.cs`

## Gap-Report Alignment
Addressed exact architecture-gap-report bullets:

- `P0`: "API keys stored plaintext in `settings.json`."
  - Mitigated by DPAPI secret storage + sanitized `settings.json` persistence.

- `P1`: "Telemetry consent not enforced at emit time."
  - Mitigated by consent gates in `NicheTelemetryService.RecordEvent` and `StructuredLogger.Write`.

## Boundary (Not in This Baseline)
- Audit log tamper-evidence/signature chain remains separate work:
  - architecture-gap-report `P1`: "Audit trail lacks tamper-evidence/signature chain."
