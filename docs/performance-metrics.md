# Performance Metrics

## Scope
This change instruments the reflex translation flow end-to-end and adds a short-segment latency budget enforcement path.

## Metrics
- `capture_to_request_start_ms`: Time from text capture (`Ctrl+Space`/in-place capture) to provider request start.
- `provider_roundtrip_ms`: Provider orchestration roundtrip (includes resilience policy + fallback attempts).
- `response_to_render_ms`: Time from translation response receipt to UI render completion.
- `end_to_end_ms`: Time from capture to render completion.
- Rolling aggregates:
  - `p50` and `p95` are computed on a rolling window (`ReflexLatencyMetricsService`, default window: 200 samples).
  - If short-segment samples exist, aggregates are computed from short-segment samples; otherwise from all samples.

## Implementation Map
- Metrics model and rolling aggregator:
  - `Segment/Models/ReflexLatencySample.cs`
  - `Segment/Models/ReflexLatencySnapshot.cs`
  - `Segment/Services/ReflexLatencyMetricsService.cs`
- Reflex flow instrumentation points:
  - `Segment/Views/FloatingPanel.xaml.cs`
  - `Segment/Views/FloatingPanel.xaml`
- Provider latency budget enforcement and retry parameterization:
  - `Segment/Services/TranslationService.cs`
  - `Segment/Services/TranslationProviderOrchestrator.cs`
  - `Segment/Services/ProviderResiliencePolicy.cs`
  - `Segment/Services/ProviderExecutionOptions.cs`
  - `Segment/Models/TranslationExecutionResult.cs`
  - `Segment/Models/TranslationProviderRequest.cs`
  - `Segment/Models/TranslationProviderResult.cs`

## Structured Logging
`ReflexLatencyMetricsService.Record(...)` emits:
- `reflex_latency_event` on each sample with all metric fields and budget/fallback flags.
- `reflex_latency_summary` every 10 samples with rolling `end_to_end_p50_ms` and `end_to_end_p95_ms`.

Logs are written through `StructuredLogger` to `%AppData%\SegmentApp\structured_events.jsonl`.

## Local Dashboard/Warning
- `FloatingPanel` shows live `Latency p50/p95` summary (`LatencySummaryText`).
- Warning banner is shown when short-segment rolling `p95 > 700ms` (`LatencyWarningText`).

## Enforcement Behavior (Short Segments)
- Short-segment mode is enabled in `TranslationService` for inputs `<= 220` chars.
- Request budget is set to `700ms`.
- Orchestrator enforces budget:
  - per-provider remaining budget timeout
  - retries disabled (`MaxRetriesOverride = 0`)
  - explicit `BudgetExceeded` failure when budget is exhausted
  - fallback provider chain is still used when there is remaining budget

## How To Inspect
1. Trigger reflex translation repeatedly with short segments.
2. Observe `Latency p50/p95` and warning state in the floating panel.
3. Inspect `%AppData%\SegmentApp\structured_events.jsonl` for:
   - `reflex_latency_event`
   - `reflex_latency_summary`
4. Confirm budget flags in logs:
   - `budget_enforced`
   - `budget_exceeded`
   - `used_fallback_provider`

## Tests
- Rolling aggregates and deterministic percentile behavior:
  - `Segment.Tests/ReflexLatencyMetricsServiceTests.cs`
- Event and summary metric emission:
  - `Segment.Tests/ReflexLatencyMetricsServiceTests.cs`
- Short-segment retry/budget policy:
  - `Segment.Tests/TranslationProviderOrchestratorTests.cs`
  - `Segment.Tests/ProviderResiliencePolicyTests.cs`

## Gap-Report Alignment
Addressed exact gap-report bullets:

- `P0`: "p95<700ms objective has no enforceable control path and conflicts with configured timeout/retry envelope."

Notes:
- No privacy/consent (`P1`) bullets are changed in this performance PR.
