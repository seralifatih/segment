# Niche Architecture Production Readiness Checklist

Date: 2026-02-17

## Resiliency
- [x] Provider calls routed through retry + timeout + circuit-breaker policy.
- [x] Cancellation token support in translation request pipeline.
- [x] Circuit-open behavior returns explicit recoverable error message.

## Performance
- [x] Translation response cache enabled.
- [x] Cache key includes: source text hash, source/target language pair, glossary version token, and active domain.
- [x] Benchmark guard added for guardrail validation p50/p95 latency on short segments.

## Startup Readiness
- [x] Startup warmup call added for selected provider route.
- [x] Warmup failures are logged and non-blocking.

## Observability and Safety
- [x] Structured logging added with JSONL sink.
- [x] PII-safe scrubbing enabled (emails and long numeric identifiers are redacted in logs).
- [x] Telemetry and logs avoid raw translation text payloads.

## UX Fallback Safety
- [x] Overlay flow includes crash-safe fallback path.
- [x] On critical failure, source text is restored for manual recovery.
- [x] Fallback path is non-destructive and keeps user in control.

## Verification
- [x] Unit tests for resiliency policy and cache/logging helpers.
- [x] Performance test for latency regression guard (p50/p95).
- [x] Full test suite run passed during hardening pass.

