# Reflex Flow State Machine

## Scope
Hardens the reflex workflow for:
- clipboard collision protection
- mode confusion reduction via explicit states
- accidental overwrite prevention
- visible timeout/failure handling
- configurable hotkeys with conflict detection
- IME/international keyboard safety checks

## State Machine
States:
- `captured`
- `translating`
- `ready`
- `applied`
- `error`

Implementation:
- `Segment/Models/OverlayWorkflowState.cs`
- `Segment/Services/OverlayWorkflowController.cs`
- `Segment/Views/FloatingPanel.xaml.cs`

Allowed transitions:
- `captured -> translating|error`
- `translating -> ready|error`
- `ready -> applied|translating|error`
- `applied -> captured|ready|translating|error`
- `error -> captured|translating`

## Diagram
```text
captured -> translating -> ready -> applied
    |           |            |        |
    v           v            v        v
  error <-------+------------+--------+
    |
    +-> captured / translating
```

## Unified Guardrail Path
Both overlay apply and in-place apply now use the same guarded path:
1. Build `TranslationContext`.
2. Evaluate via `TranslationPastebackCoordinator`.
3. Block to `error` + issue panel if blocking guardrails exist.
4. Apply only if guardrails pass (or policy-allowed override).

Files:
- `Segment/Views/FloatingPanel.xaml.cs`
- `Segment/Services/TranslationPastebackCoordinator.cs`

## Clipboard Collision + Overwrite Protection
- Before writing translated output, compare expected clipboard snapshot vs current clipboard.
- If changed, abort apply and transition to `error`.
- Reason shown in UI (`Paste cancelled: ...`).

Files:
- `Segment/Services/ClipboardSafetyService.cs`
- `Segment/Models/ClipboardCollisionDecision.cs`
- `Segment/Views/FloatingPanel.xaml.cs`

## Timeout and Failure Visibility
- Translation errors transition to `error`.
- Timeout-specific notification is explicit (`Translation Timeout` dialog).

Files:
- `Segment/Views/FloatingPanel.xaml.cs`
- `Segment/Services/ProviderResiliencePolicy.cs`

## Undo-Safe Paste
- Apply flow stores pre-apply clipboard snapshot.
- `Undo Paste` restores prior clipboard in one click.

Files:
- `Segment/Views/FloatingPanel.xaml.cs`
- `Segment/Views/FloatingPanel.xaml`

## Hotkey Configuration and Conflict Detection
- Settings now persist:
  - `ShowPanelHotkey`
  - `TranslateSelectionInPlaceHotkey`
- Parse/validate hotkeys and detect collisions before save.
- Runtime hotkey registration restores safe defaults when conflicts are detected.

Files:
- `Segment/Services/SettingsService.cs`
- `Segment/Services/HotkeyBindingService.cs`
- `Segment/Models/HotkeyBinding.cs`
- `Segment/Views/SettingsWindow.xaml`
- `Segment/Views/SettingsWindow.xaml.cs`
- `Segment/App.xaml.cs`

## IME / International Keyboard Handling
- Key handlers skip reflex shortcuts during IME composition keys:
  - `ImeProcessed`
  - `DeadCharProcessed`
- Prevents accidental trigger while composing characters.

Files:
- `Segment/Views/FloatingPanel.xaml.cs`

## Failure Handling Summary
- Guardrail blocking: show blocking issues, no apply.
- Clipboard collision: abort apply to prevent overwrite.
- Provider timeout: explicit timeout dialog and error state.
- In-place failure: panel is shown with error instead of silent fallback.

## Tests
- State transitions + timeout error state:
  - `Segment.Tests/OverlayWorkflowControllerTests.cs`
- Accidental overwrite protection:
  - `Segment.Tests/ClipboardSafetyServiceTests.cs`
- Hotkey parse/conflict detection:
  - `Segment.Tests/HotkeyBindingServiceTests.cs`
- Guardrail blocking behavior remains covered:
  - `Segment.Tests/TranslationPastebackGuardIntegrationTests.cs`

## Gap-Report Alignment
Addressed exact architecture-gap-report bullets:

- `P0`: "In-place fallback bypasses terminology/QA guardrails and can inject unchecked output into external systems."
  - Fixed by routing in-place apply through the same `TranslationPastebackCoordinator` path as overlay.

- `P1`: "Guardrail override path in panel not bound to `AllowGuardrailOverrides` policy."
  - Fixed by policy-gating override button visibility and handler execution in `FloatingPanel`.
