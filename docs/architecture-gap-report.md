# Architecture Gap Report

## Current Architecture Map (components + data flow)

### Runtime translation path
1. Global hotkeys (`Ctrl+Space`, `Ctrl+Shift+Space`) are registered in app startup and routed to `FloatingPanel`. (`Segment/App.xaml.cs:88`, `Segment/App.xaml.cs:89`, `Segment/App.xaml.cs:325`, `Segment/App.xaml.cs:337`)
2. `FloatingPanel` captures source text from clipboard and starts translation telemetry timing. (`Segment/Views/FloatingPanel.xaml.cs:129`, `Segment/Views/FloatingPanel.xaml.cs:220`, `Segment/Views/FloatingPanel.xaml.cs:235`)
3. `TranslationService.SuggestAsync` resolves provider route/confidentiality mode, computes glossary-versioned cache key, checks in-memory translation cache, and logs routing decisions to compliance audit. (`Segment/Services/TranslationService.cs:44`, `Segment/Services/TranslationService.cs:47`, `Segment/Services/TranslationService.cs:54`, `Segment/Services/TranslationService.cs:65`)
4. For redacted-cloud mode, text is transformed through `TextRedactionService` before provider call. (`Segment/Services/TranslationService.cs:96`, `Segment/Services/TextRedactionService.cs:16`)
5. Provider call goes through resilience policy (timeouts/retries/circuit breaker) and then to Google/Ollama/Custom endpoints. (`Segment/Services/TranslationService.cs:122`, `Segment/Services/ProviderResiliencePolicy.cs:65`, `Segment/Services/TranslationService.cs:373`, `Segment/Services/TranslationService.cs:416`, `Segment/Services/TranslationService.cs:460`)
6. Successful results are cached in-process with TTL. (`Segment/Services/TranslationService.cs:140`, `Segment/Services/TranslationResultCacheService.cs:16`, `Segment/Services/TranslationResultCacheService.cs:54`)
7. Normal overlay path validates output via domain guardrails before pasteback; blocking issues require override reason dialog. (`Segment/Views/FloatingPanel.xaml.cs:350`, `Segment/Services/TranslationPastebackCoordinator.cs:14`, `Segment/Services/TranslationGuardrailEngine.cs:35`, `Segment/Views/FloatingPanel.xaml.cs:586`)
8. In-place fallback path directly pastes translated output via clipboard/SendKeys and skips guardrail evaluation. (`Segment/Views/FloatingPanel.xaml.cs:164`, `Segment/Views/FloatingPanel.xaml.cs:206`, `Segment/Views/FloatingPanel.xaml.cs:207`)

### Terminology and glossary path
1. Glossary data is persisted in LiteDB (`glossary.db`) under `%AppData%\\SegmentApp`; profiles are global + project scoped. (`Segment/Services/GlossaryService.cs:31`, `Segment/Services/GlossaryService.cs:38`, `Segment/Services/GlossaryService.cs:42`)
2. Effective terminology merges Global then Current Project (project overwrites global), with in-memory read-through cache. (`Segment/Services/GlossaryService.cs:198`, `Segment/Services/GlossaryService.cs:212`, `Segment/Services/GlossaryService.cs:219`, `Segment/Services/GlossaryService.cs:227`)
3. Locked terminology for prompts is resolved by domain/language/scope/priority ordering and injected into prompt policy. (`Segment/Services/TranslationService.cs:334`, `Segment/Services/GlossaryResolverService.cs:22`, `Segment/Services/GlossaryResolverService.cs:40`, `Segment/Services/PromptPolicyComposer.cs:60`)
4. Resolver conflicts are persisted for diagnostics/audit trail. (`Segment/Services/GlossaryResolverService.cs:51`, `Segment/Services/GlossaryService.cs:356`)

### Learning loop path
1. After paste, `LearningManager` watches clipboard every second for user edits and detects terminology-like deltas. (`Segment/Views/FloatingPanel.xaml.cs:391`, `Segment/Services/LearningManager.cs:31`, `Segment/Services/LearningManager.cs:64`, `Segment/Services/TermDetective.cs:19`)
2. A learning widget requests lemma alignment through another model call, then writes term updates to glossary (global/project) with conflict prompts and compliance audit records. (`Segment/Views/LearningWidget.xaml.cs:41`, `Segment/Services/LemmaService.cs:39`, `Segment/Views/LearningWidget.xaml.cs:92`, `Segment/Views/LearningWidget.xaml.cs:172`, `Segment/Views/LearningWidget.xaml.cs:114`)

### Settings/key handling path
1. All runtime settings (including API keys and consent flags) are stored in local `settings.json` in plaintext. (`Segment/Services/SettingsService.cs:14`, `Segment/Services/SettingsService.cs:24`, `Segment/Services/SettingsService.cs:68`, `Segment/Services/SettingsService.cs:85`)
2. Settings UI loads/saves provider keys directly from password controls to config object. (`Segment/Views/SettingsWindow.xaml.cs:77`, `Segment/Views/SettingsWindow.xaml.cs:84`, `Segment/Views/SettingsWindow.xaml.cs:182`, `Segment/Views/SettingsWindow.xaml.cs:189`)

### Telemetry/crash/audit path
1. Niche telemetry events are persisted in LiteDB (`niche_telemetry.db`) with hashed segment IDs and latency/guardrail metrics. (`Segment/Services/NicheTelemetryService.cs:20`, `Segment/Services/NicheTelemetryService.cs:31`, `Segment/Services/NicheTelemetryService.cs:104`, `Segment/Services/NicheTelemetryService.cs:141`)
2. Overlay emits telemetry events for request/complete/glossary/guardrail/paste states; failures are swallowed to protect UX. (`Segment/Views/FloatingPanel.xaml.cs:181`, `Segment/Views/FloatingPanel.xaml.cs:229`, `Segment/Views/FloatingPanel.xaml.cs:322`, `Segment/Views/FloatingPanel.xaml.cs:511`, `Segment/Views/FloatingPanel.xaml.cs:537`)
3. Compliance audit records are appended to local JSONL and exportable to CSV/JSONL. (`Segment/Services/ComplianceAuditService.cs:25`, `Segment/Services/ComplianceAuditService.cs:37`, `Segment/Services/ComplianceAuditService.cs:88`, `Segment/Views/SettingsWindow.xaml.cs:790`)
4. Global crash handlers append full exception details to `%AppData%\\SegmentApp\\crash_log.txt`. (`Segment/App.xaml.cs:40`, `Segment/App.xaml.cs:134`, `Segment/App.xaml.cs:155`)

## Gaps vs Requirements

### 1) Deterministic terminology control

**What exists**
- Deterministic candidate ranking (scope > priority > recency > createdAt > target) is implemented in resolver. (`Segment/Services/GlossaryResolverService.cs:40`)
- Prompt receives explicit locked glossary mappings. (`Segment/Services/PromptPolicyComposer.cs:60`)
- Guardrail can block locked-term violations in validated path. (`Segment/Services/LegalDomainQaPlugin.cs:37`, `Segment/Views/FloatingPanel.xaml.cs:350`)

**Gaps**
- `Ctrl+Shift+Space` in-place flow bypasses guardrail validation and force-pastes raw model output, so terminology is not deterministically enforced on that path. (`Segment/Views/FloatingPanel.xaml.cs:164`, `Segment/Views/FloatingPanel.xaml.cs:185`, `Segment/Views/FloatingPanel.xaml.cs:206`)
- Override on blocked guardrails is always available from UI and handler path; it is not gated by `AllowGuardrailOverrides` in this flow. (`Segment/Views/FloatingPanel.xaml:115`, `Segment/Views/FloatingPanel.xaml.cs:586`, `Segment/Services/SettingsService.cs:55`)
- Learning loop uses model-based lemma extraction (`TranslationService.SuggestAsync(prompt)`) before glossary write, introducing non-deterministic term canonicalization. (`Segment/Services/LemmaService.cs:10`, `Segment/Services/LemmaService.cs:39`, `Segment/Views/LearningWidget.xaml.cs:41`)

### 2) p95 latency <700ms for short segments

**What exists**
- Translation timings are captured and aggregated to p50/p95 in telemetry snapshots. (`Segment/Views/FloatingPanel.xaml.cs:235`, `Segment/Services/NicheTelemetryService.cs:141`)
- In-memory translation cache can eliminate repeated calls for identical text/domain/glossary version. (`Segment/Services/TranslationService.cs:54`, `Segment/Services/TranslationResultCacheService.cs:68`)

**Gaps**
- No runtime SLO enforcement or admission control exists for the 700ms target; system only records latency after the fact. (`Segment/Views/FloatingPanel.xaml.cs:235`, `Segment/Services/NicheTelemetryService.cs:141`)
- Retry/timeout policy allows multi-second to multi-tens-of-seconds behavior (`15s` per attempt, retries up to 3 attempts), incompatible with strict p95 <700ms for uncached requests. (`Segment/Services/ProviderResiliencePolicy.cs:27`, `Segment/Services/ProviderResiliencePolicy.cs:65`, `Segment/Services/ProviderResiliencePolicy.cs:69`)
- Provider calls are synchronous request/response with remote APIs and no short-segment fast-path model/routing split. (`Segment/Services/TranslationService.cs:373`, `Segment/Services/TranslationService.cs:416`, `Segment/Services/TranslationService.cs:460`)
- Existing performance benchmark focuses on guardrail engine only, not end-to-end translation latency budget. (`Segment.Tests/GuardrailLatencyBenchmarkTests.cs:14`, `Segment.Tests/GuardrailLatencyBenchmarkTests.cs:33`)

### 3) Enterprise-grade privacy + auditability

**What exists**
- Confidential routing modes can block cloud routes or apply redaction before cloud call. (`Segment/Services/TranslationService.cs:224`, `Segment/Services/TranslationService.cs:240`, `Segment/Services/TextRedactionService.cs:16`)
- Compliance audit trail exists for routing, glossary conflicts, and guardrail overrides. (`Segment/Services/TranslationService.cs:65`, `Segment/Views/LearningWidget.xaml.cs:114`, `Segment/Views/FloatingPanel.xaml.cs:599`)
- Telemetry avoids raw segment storage by hashing. (`Segment/Services/NicheTelemetryService.cs:31`, `Segment.Tests/NicheTelemetryServiceTests.cs:49`)

**Gaps**
- API secrets are persisted unencrypted in local JSON settings. (`Segment/Services/SettingsService.cs:14`, `Segment/Services/SettingsService.cs:24`, `Segment/Services/SettingsService.cs:85`)
- Telemetry consent flags exist but are not enforced in telemetry emission path; events are recorded regardless of consent booleans. (`Segment/Services/SettingsService.cs:59`, `Segment/Views/FloatingPanel.xaml.cs:511`, `Segment/Services/NicheTelemetryService.cs:62`)
- Crash logging writes full exception text/stack to local file without redaction/consent gate, risking sensitive leakage in diagnostics artifacts. (`Segment/App.xaml.cs:134`, `Segment/App.xaml.cs:144`, `Segment/App.xaml.cs:155`)
- Audit logs are append-only files but lack tamper evidence/signing/chain integrity controls required for enterprise-grade non-repudiation. (`Segment/Services/ComplianceAuditService.cs:50`, `Segment/Services/ComplianceAuditService.cs:53`)

### 4) Interoperability-first (augment CAT/TMS, don’t replace)

**What exists**
- Current product already behaves as an overlay/augmentation tool by operating on user clipboard and pasting back into host app context. (`Segment/Views/FloatingPanel.xaml.cs:129`, `Segment/Views/FloatingPanel.xaml.cs:169`, `Segment/Views/FloatingPanel.xaml.cs:207`)
- TMX import supports ingesting existing translation memories. (`Segment/Services/TmxImportService.cs:13`, `Segment/Views/SettingsWindow.xaml.cs:250`)

**Gaps**
- No native CAT/TMS connectors or API-based integrations are present in translation execution path; interoperability is limited to clipboard automation and file import/export. (`Segment/Views/FloatingPanel.xaml.cs:169`, `Segment/Services/TranslationService.cs:122`, `Segment/Views/SettingsWindow.xaml.cs:250`)
- No XLIFF/segment-state interchange or project sync protocol exists for round-trip status with external CAT/TMS systems. (No implementation under `Segment/Services` beyond TMX import; import path only in `Segment/Services/TmxImportService.cs:13`)
- `SendKeys`-based pasteback is fragile for enterprise workflows (focus/race/security restrictions), weakening reliable augmentation posture. (`Segment/Views/FloatingPanel.xaml.cs:169`, `Segment/Views/FloatingPanel.xaml.cs:207`)

## Risk Ranking (P0/P1/P2)

### P0
- In-place fallback bypasses terminology/QA guardrails and can inject unchecked output into external systems. (`Segment/Views/FloatingPanel.xaml.cs:164`, `Segment/Views/FloatingPanel.xaml.cs:206`)
- API keys stored plaintext in `settings.json`. (`Segment/Services/SettingsService.cs:14`, `Segment/Services/SettingsService.cs:85`)
- p95<700ms objective has no enforceable control path and conflicts with configured timeout/retry envelope. (`Segment/Services/ProviderResiliencePolicy.cs:27`, `Segment/Services/ProviderResiliencePolicy.cs:65`)

### P1
- Telemetry consent not enforced at emit time. (`Segment/Services/SettingsService.cs:59`, `Segment/Views/FloatingPanel.xaml.cs:511`)
- Guardrail override path in panel not bound to `AllowGuardrailOverrides` policy. (`Segment/Views/FloatingPanel.xaml:115`, `Segment/Views/FloatingPanel.xaml.cs:586`, `Segment/Services/SettingsService.cs:55`)
- Audit trail lacks tamper-evidence/signature chain. (`Segment/Services/ComplianceAuditService.cs:50`, `Segment/Services/ComplianceAuditService.cs:53`)

### P2
- Learning lemma extraction is model-dependent and can introduce inconsistent canonical terms over time. (`Segment/Services/LemmaService.cs:39`, `Segment/Views/LearningWidget.xaml.cs:41`)
- Interoperability depth is low (TMX import + clipboard), with no structured CAT/TMS sync contract. (`Segment/Services/TmxImportService.cs:13`, `Segment/Views/FloatingPanel.xaml.cs:169`)

## Recommended Implementation Order

### 2 weeks (stabilize critical risk)
- Unify translation paths so in-place flow also runs `TranslationPastebackCoordinator`/guardrails before pasteback. (`Segment/Views/FloatingPanel.xaml.cs:164`, `Segment/Views/FloatingPanel.xaml.cs:350`)
- Enforce `AllowGuardrailOverrides` in overlay override UI and handler logic, not only in learning widget. (`Segment/Views/FloatingPanel.xaml.cs:586`, `Segment/Views/LearningWidget.xaml.cs:64`)
- Move API key storage to OS-protected secret storage (DPAPI/Credential Manager) and keep `settings.json` with references only. (`Segment/Services/SettingsService.cs:14`, `Segment/Services/SettingsService.cs:85`)
- Add consent gate checks before telemetry/crash persistence writes. (`Segment/Views/FloatingPanel.xaml.cs:511`, `Segment/App.xaml.cs:134`)

### 1 month (meet targetability)
- Implement latency-budgeted routing for short segments (local-first or fast model tier), with hard cutoff/cancel policy aligned to 700ms SLO instead of 15s attempt timeout. (`Segment/Services/ProviderResiliencePolicy.cs:27`, `Segment/Services/TranslationService.cs:122`)
- Add end-to-end latency benchmark/integration test around `FloatingPanel -> TranslationService -> guardrail -> paste decision` with p95 assertion for short segments. (`Segment.Tests/GuardrailLatencyBenchmarkTests.cs:14`)
- Make audit logs tamper-evident (hash chain + periodic signature checkpoints) and include actor/session identifiers consistently. (`Segment/Services/ComplianceAuditService.cs:37`, `Segment/Services/ComplianceAuditService.cs:50`)

### 3 months (interoperability-first maturity)
- Introduce explicit CAT/TMS integration layer (connector interfaces + adapters) while retaining overlay as fallback channel. (Current absence evidenced by clipboard-centric flow in `Segment/Views/FloatingPanel.xaml.cs:169` and provider-only translation service in `Segment/Services/TranslationService.cs:122`)
- Add structured interchange support beyond TMX (at minimum XLIFF import/export + segment status metadata). (Current TMX-only import: `Segment/Services/TmxImportService.cs:13`)
- Formalize deterministic terminology governance: immutable per-request terminology snapshot IDs, policy-driven override approvals, and deterministic learning pipeline without model-dependent lemma extraction on critical accounts. (`Segment/Services/TranslationService.cs:46`, `Segment/Services/LemmaService.cs:39`, `Segment/Views/LearningWidget.xaml.cs:153`)
