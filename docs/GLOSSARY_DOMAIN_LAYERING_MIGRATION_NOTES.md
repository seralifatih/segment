# Glossary Domain Layering Migration Notes

## Summary
The glossary model was extended to support deterministic domain-aware resolution:
- `DomainVertical`
- `SourceLanguage` / `TargetLanguage`
- `ScopeType` (`System`, `User`, `Team`, `Project`, `Session`)
- `Priority`
- `LastAcceptedAt`

## Backward Compatibility
- Existing `TermEntry` records remain readable without destructive migration.
- Missing metadata is inferred at runtime:
  - Global profile terms default to `ScopeType=User`
  - Project/profile terms default to `ScopeType=Project`
  - `SourceLanguage` defaults to `English`
  - `TargetLanguage` defaults to current app target language
  - `DomainVertical` defaults to active app domain (fallback `Legal`)

## Conflict Audit Trail
- Resolver conflict events are persisted to `glossary_resolution_conflicts` in the glossary database.
- Each record stores winner selection and reason for deterministic traceability.

## Operational Notes
- Resolver behavior:
  - exact source-term match first
  - fallback by normalized source-term containment
  - deterministic ranking:
    1. scope rank (`Project > Team > User > System`, with `Session` highest)
    2. priority
    3. last accepted timestamp
    4. created timestamp
    5. lexical tie-break
