# Prompt Safety

## Threat Model
Prompt pipeline risks addressed in this change:
- Source text contains prompt-injection payloads (e.g., `ignore previous instructions`).
- Glossary constraints carry poisoned instruction-like strings.
- Learned terminology candidates contain adversarial content and get promoted.
- Shared/global glossary writes happen without explicit human approval.

## Implemented Controls

### 1) Centralized sanitizer
- Added `PromptSafetySanitizer` for source text and glossary constraints:
  - `Segment/Services/PromptSafetySanitizer.cs`
- Applies:
  - control-char cleanup
  - role-tag neutralization (`system:`/`assistant:` etc.)
  - instruction-like payload detection/blocking
  - glossary lock filtering

### 2) Source text always untrusted
- Source text sanitized before provider request:
  - `Segment/Services/TranslationService.cs`
- Provider prompts now wrap source in explicit untrusted data block:
  - `Segment/Services/TranslationProviders.cs`
- Prompt policy explicitly states source/glossary instructions must not be executed:
  - `Segment/Services/PromptPolicyComposer.cs`

### 3) Learned-term payload stripping and gating
- Lemma inputs/outputs sanitized:
  - `Segment/Services/LemmaService.cs`
- Learning save path blocks instruction-like term candidates:
  - `Segment/Views/LearningWidget.xaml.cs`

### 4) Confidence/reputation thresholds for suggestion
- Added `TermLearningSafetyEvaluator`:
  - `Segment/Services/TermLearningSafetyEvaluator.cs`
  - `Segment/Models/TermPromotionAssessment.cs`
- `LearningManager` now gates promotion suggestions before toast:
  - `Segment/Services/LearningManager.cs`
- Promotion remains suggest-only by default (toast + explicit Save action).

### 5) Shared/high-scope promotion requires explicit approval
- Global/shared promotion now requires explicit approval flow with required reason:
  - `Segment/Views/LearningWidget.xaml.cs`
- Denied/approved promotion decisions are recorded via compliance audit events.

## Adversarial Test Cases
- Source and glossary sanitization:
  - `Segment.Tests/PromptSafetySanitizerTests.cs`
- Promotion safety thresholds and injection blocking:
  - `Segment.Tests/TermLearningSafetyEvaluatorTests.cs`
- Learning path adversarial candidate blocked:
  - `Segment.Tests/LearningManagerTests.cs`
- Prompt composition lock filtering:
  - `Segment.Tests/PromptPolicyComposerTests.cs`

## Gap-Report Alignment
Exact architecture-gap-report bullets addressed by this change:

- No new `P0` or `P1` bullet is directly closed by this prompt-safety hardening change.
- Directly addressed (`P2`):
  - "Learning lemma extraction is model-dependent and can introduce inconsistent canonical terms over time."
  - Mitigation: sanitization + confidence/reputation gating + explicit shared promotion approval path.
