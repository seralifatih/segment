# QA Guardrails

## Overview
This guardrail layer runs **before final apply/paste** and augments domain guardrails with deterministic QA checks:
- glossary adherence
- number/date consistency
- punctuation/tag parity

It is executed via `TranslationPastebackCoordinator` and merges QA issues with existing domain guardrail output.

## Checks

### 1) Glossary adherence
- Rule: `QA_GLOSSARY_ADHERENCE`
- Trigger: source contains a locked term from `TranslationContext.LockedTerminology`, but target does not contain required glossary target.
- Default severity: `Warning` (`SeverityScore=70`)
- Suggested fix: enforce locked term target exactly.

Example:
- Source: `The governing law is Turkish law.`
- Locked: `governing law -> uygulanacak hukuk`
- Target: `Bu sozlesme Turk hukukuna tabidir.`
- Result: warning (missing `uygulanacak hukuk`).

### 2) Number/date consistency
- Rules: `QA_NUMBER_CONSISTENCY`, `QA_DATE_CONSISTENCY`
- Trigger: numeric/date token sequence mismatch between source and translation.
- Default severity: `Warning` (`SeverityScore=65`)

Example:
- Source: `Payment of 1200 is due on 2026-03-01.`
- Target: `Odeme 1100 tutarinda ve 2026-04-01 tarihinde...`
- Result: numeric and date warnings.

### 3) Punctuation/tag parity
- Rules: `QA_PUNCTUATION_PARITY`, `QA_TAG_PARITY`
- Trigger:
  - sentence-final punctuation mismatch
  - markup tag sequence mismatch
- Default severities:
  - punctuation: `Warning` (`SeverityScore=55`)
  - tag parity: `Error` blocking (`SeverityScore=90`)

Example:
- Source: `Click <b>Save</b> now.`
- Target: `Simdi kaydetin!`
- Result: punctuation warning + blocking tag parity issue.

## Strict Mode
- Setting: `SettingsService.Current.QaStrictMode`
- UI: `Strict QA mode for legal/regulatory workflows`
- Behavior: for `Legal`, `Medical`, `Financial` domains, warning-level QA issues are promoted to blocking.

This supports legal/regulatory workflows where minor drift is unacceptable.

## Overlay UX
When only non-blocking QA warnings exist:
- overlay shows pre-apply warning summary
- one-keystroke actions:
  - `R` = review issues
  - `A` = apply anyway

When blocking issues exist, existing override-with-reason flow remains in effect.

## Tuning Guidance
- Use default mode for fast editing workflows to avoid over-blocking.
- Enable strict mode for legal/regulatory projects with formal QA requirements.
- Keep locked terminology focused on high-value terms to reduce noisy warnings.
- Treat `QA_TAG_PARITY` as always blocking for structured/markup-heavy content.

## Implementation References
- `Segment/Services/TranslationQaService.cs`
- `Segment/Services/TranslationPastebackCoordinator.cs`
- `Segment/Views/FloatingPanel.xaml.cs`
- `Segment/Views/FloatingPanel.xaml`
- `Segment/Services/SettingsService.cs`
- `Segment/Views/SettingsWindow.xaml`
- `Segment/Views/SettingsWindow.xaml.cs`
