# Learning Consent Flow

## Goal
Improve trust in the post-edit learning loop by requiring explicit consent, preventing silent overwrite, and surfacing collisions with a keyboard-first assistant.

## Interaction States
1. `detected_change`
- Trigger: `LearningManager` detects an eligible post-edit term delta.
- Action: `INotificationService.ShowToast(change)` opens consent toast.

2. `consent_prompt`
- Prompt text: `Save as preferred translation?`
- Choices:
  - `Always` -> save to global scope.
  - `This project` -> save to current project scope.
  - `Not now` -> no glossary write.
- Keyboard:
  - `A` => Always
  - `P` => This project
  - `N` or `Esc` => Not now

3. `conflict_assistant` (conditional)
- Trigger: same source term already exists in selected scope with different target.
- UI: side-by-side existing vs new suggestion in `TermConflictAssistantWindow`.
- Required user choice (no silent overwrite):
  - `Keep existing`
  - `Use new`
  - `Cancel`
- Keyboard:
  - `Left` => Keep existing
  - `Right` => Use new
  - `Esc` => Cancel

4. `persisted_or_deferred`
- Persisted:
  - `GlossaryService.AddTerm(...)`
  - usage log action: `learning_saved_global` or `learning_saved_project`
- Deferred:
  - no glossary write for `Not now`.

## Weekly Digest Model/Service
- Service: `LearningDigestService` (`ILearningDigestService`)
- Window: last 7 days.
- Metrics:
  - `terms_learned`: count of successful usage actions `learning_saved_global` + `learning_saved_project`.
  - `unresolved_conflicts`: conflict records with empty winner or reason containing `unresolved`/`collision`.
- Model: `WeeklyLearningDigest`
  - `WindowStartUtc`, `WindowEndUtc`
  - `TermsLearned`
  - `UnresolvedConflicts`
  - `LearnedTerms`

## Failure/Recovery Behavior
- If sanitization yields empty source/target, consent result is skipped and no write occurs.
- If conflict resolver is unavailable, service returns `RequiresConflictResolution = true`; no overwrite occurs.
- If user cancels conflict dialog, operation is skipped.

## Gap-Report Alignment
Exact P0/P1 bullets addressed from `docs/architecture-gap-report.md`:

- `P0`: none directly addressed in this change.
- `P1`: none directly addressed in this change.

Related non-P0/P1 trust hardening covered by this implementation:
- Prevents silent glossary overwrite by requiring explicit conflict resolution before any overwrite in learning flow.
